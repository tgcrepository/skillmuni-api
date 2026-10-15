using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Domain;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Services;

/// <summary>
/// API-068…074: My Job Profile (the Living CV). Lives in Learner because Learner owns the quest answers it renders
/// (A-262: one record built from the 8 quests). Edits to sections go through the quests themselves (FR-12).
/// </summary>
public interface IJobProfileService
{
    Task<JobProfileDto> GetAsync(LearnerRef who, CancellationToken cancellationToken);

    Task<CvContactDto> GetCvContactAsync(LearnerRef who, CancellationToken cancellationToken);

    Task<JobProfileDto> SaveAboutAsync(LearnerRef who, SaveAboutRequest request, CancellationToken cancellationToken);

    /// <summary>Stores the newer CV and returns what it would change; nothing is applied yet (FR-14).</summary>
    Task<Result<IReadOnlyList<CvChangeDto>>> UploadCvAsync(LearnerRef who, UploadedFile? file, CancellationToken cancellationToken);

    Task<JobProfileDto> ApplyChangesAsync(LearnerRef who, ApplyCvChangesRequest request, CancellationToken cancellationToken);

    Task<ShareSettingsDto> GetShareAsync(LearnerRef who, CancellationToken cancellationToken);

    Task<ShareSettingsDto> UpdateShareAsync(LearnerRef who, UpdateShareRequest request, CancellationToken cancellationToken);
}

public sealed class JobProfileService(
    ILearnerAccessor accessor,
    IQuestAnswersRepository answers,
    IJobProfileRepository jobProfiles,
    IProfileProgressService profileProgress,
    IBlobStore blobs,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<LearnerServiceOptions> options) : IJobProfileService
{
    private JobProfileOptions Settings => options.Value.JobProfile;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<JobProfileDto> GetAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var settings = await SettingsAsync(learner, cancellationToken);
        return await BuildAsync(learner, settings, cancellationToken);
    }

    public async Task<CvContactDto> GetCvContactAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var settings = await jobProfiles.FindAsync(learner.Id, cancellationToken);
        return JobProfileBuilder.Contact(learner, await answers.ListAsync(who.Id, cancellationToken), settings);
    }

    public async Task<JobProfileDto> SaveAboutAsync(LearnerRef who, SaveAboutRequest request, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var settings = await SettingsAsync(learner, cancellationToken);
        settings.RewriteAbout(request.Text, Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildAsync(learner, settings, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<CvChangeDto>>> UploadCvAsync(LearnerRef who, UploadedFile? file, CancellationToken cancellationToken)
    {
        var read = await FileSniffer.ReadAsync(file, Settings.MaxCvBytes, FileSniffer.CvFiles, cancellationToken);
        if (!read.IsSuccess)
        {
            return read.Error;
        }

        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var settings = await SettingsAsync(learner, cancellationToken);
        var blobName = $"job-profile/{who.Id:N}/cv-{Guid.NewGuid():N}";
        await using (read.Value.Content)
        {
            await blobs.UploadAsync(blobName, read.Value.Content, read.Value.ContentType, cancellationToken);
        }

        var previous = settings.ReplaceCv(blobName, Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (previous is not null)
        {
            await blobs.DeleteAsync(previous, cancellationToken);
        }

        // Suggestions come from the learner's latest CV read (cv.parse.completed); see the README.
        var all = await answers.ListAsync(who.Id, cancellationToken);
        IReadOnlyList<CvChangeDto> changes = JobProfileBuilder.Changes(learner, all, settings).Select(c => c.Change).ToList();
        return Result<IReadOnlyList<CvChangeDto>>.Success(changes);
    }

    public async Task<JobProfileDto> ApplyChangesAsync(LearnerRef who, ApplyCvChangesRequest request, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var settings = await SettingsAsync(learner, cancellationToken);
        var selected = (request.Ids ?? []).ToHashSet(StringComparer.Ordinal);
        var all = await answers.ListAsync(who.Id, cancellationToken);
        var now = Now;

        // A-207: contact changes are never applied (the record keeps the verified value).
        var toApply = JobProfileBuilder.Changes(learner, all, settings)
            .Where(c => selected.Contains(c.Change.Id) && c.Item is not null && c.ListKey.Length > 0)
            .ToList();
        foreach (var group in toApply.GroupBy(c => c.Slug, StringComparer.Ordinal))
        {
            var record = await answers.FindAsync(who.Id, group.Key, cancellationToken);
            if (record is null)
            {
                record = QuestAnswers.Start(tenant.TenantId, who.Id, group.Key, now);
                answers.Add(record);
            }

            var json = JsonNode.Parse(record.AnswersJson) as JsonObject ?? new JsonObject();
            foreach (var change in group)
            {
                if (json[change.ListKey] is not JsonArray list)
                {
                    list = new JsonArray();
                    json[change.ListKey] = list;
                }

                var item = JsonNode.Parse(change.Item!.Raw.ToJsonString())!.AsObject();
                item["fromCv"] = true;
                list.Add(item);
            }

            record.Replace(json.ToJsonString(), now);
        }

        if (toApply.Count > 0)
        {
            var applied = (JsonSerializer.Deserialize<List<string>>(settings.AppliedChangesJson) ?? [])
                .Concat(toApply.Select(c => c.Change.Id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            settings.RecordApplied(JsonSerializer.Serialize(applied), now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await BuildAsync(learner, settings, cancellationToken);
    }

    public async Task<ShareSettingsDto> GetShareAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        return Share(learner, await SettingsAsync(learner, cancellationToken));
    }

    public async Task<ShareSettingsDto> UpdateShareAsync(LearnerRef who, UpdateShareRequest request, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var settings = await SettingsAsync(learner, cancellationToken);
        settings.SetSharing(request.HideContact, request.ExpiresDays, Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Share(learner, settings);
    }

    /// <summary>Views aren't counted until the public CV page exists (viewsThisWeek is 0).</summary>
    private ShareSettingsDto Share(LearnerProfile learner, JobProfileSettings settings) =>
        new(JobProfileBuilder.ShareUrl(Settings.ShareBaseUrl, learner, settings), settings.HideContact, settings.ExpiresDays, 0);

    private async Task<JobProfileSettings> SettingsAsync(LearnerProfile learner, CancellationToken cancellationToken)
    {
        var settings = await jobProfiles.FindAsync(learner.Id, cancellationToken);
        if (settings is null)
        {
            settings = JobProfileSettings.Create(tenant.TenantId, learner.Id, ShareCodes.New(), Now);
            jobProfiles.Add(settings);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    private async Task<JobProfileDto> BuildAsync(LearnerProfile learner, JobProfileSettings settings, CancellationToken cancellationToken)
    {
        var all = await answers.ListAsync(learner.Id, cancellationToken);
        var lastAnswer = await answers.LastUpdatedAsync(learner.Id, cancellationToken);
        var updated = new[] { learner.UpdatedAt, settings.UpdatedAt, lastAnswer ?? DateTime.MinValue }.Max();
        return JobProfileBuilder.Build(new JobProfileBuilder.Input(learner, all, settings, profileProgress.Calculate(learner, all), updated));
    }
}

/// <summary>Short random codes for share links: 8 characters from an alphabet without look-alikes.</summary>
public static class ShareCodes
{
    private const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyz";
    private const int Length = 8;

    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);
}
