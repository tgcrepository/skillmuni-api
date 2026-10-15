using System.Globalization;
using System.Text.Json;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Errors;
using Learner.Application.Events;
using Learner.Domain;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Storage;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Services;

/// <summary>API-115–121 except contact changes (those are Identity's, A-273): Profile v2 · Player ID.</summary>
public interface IProfileService
{
    Task<PlayerProfileDto> GetAsync(LearnerRef who, CancellationToken cancellationToken);

    Task<RevealDto> RevealAsync(LearnerRef who, string field, CancellationToken cancellationToken);

    Task<ProfileEditDto> GetEditableAsync(LearnerRef who, CancellationToken cancellationToken);

    Task<Result<ProfileSavedDto>> SaveAsync(LearnerRef who, ProfileEditRequest edit, CancellationToken cancellationToken);

    Task<Result<ProfileSavedDto>> UploadPhotoAsync(LearnerRef who, UploadedFile? photo, CancellationToken cancellationToken);

    Task<Result<StoredBlob>> GetPhotoAsync(Guid learnerId, CancellationToken cancellationToken);
}

public sealed class ProfileService(
    ILearnerAccessor accessor,
    ILearnerRepository learners,
    ILinkedIdentityRepository identities,
    IBlobStore blobs,
    IProgressService progress,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<LearnerServiceOptions> options) : IProfileService
{
    public const string PhotoPath = "/api/v1/profile/photo";

    // Strength weights from the design note: contact 30 · name 30 · photo 10 · skills 15 · Career Drivers 15.
    private static readonly (string Id, string Label, int Weight)[] StrengthTasks =
    [
        ("contact", "Contact details", 30),
        ("name", "Name & joining date", 30),
        ("photo", "Add a profile photo", 10),
        ("skills", "Add your top skills", 15),
        ("drivers", "Take Career Drivers", 15),
    ];

    private XpRules Xp => options.Value.Xp;

    public static string? PhotoUrl(LearnerProfile learner) =>
        learner.HasPhoto ? $"{PhotoPath}?v={learner.PhotoVersion.ToString(CultureInfo.InvariantCulture)}" : null;

    public async Task<PlayerProfileDto> GetAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        return await BuildAsync(learner, cancellationToken);
    }

    public async Task<RevealDto> RevealAsync(LearnerRef who, string field, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var value = field switch
        {
            "mobile" => ProfileFormat.DisplayPhone(learner.Phone),
            "email" => learner.Email ?? string.Empty,
            "gender" => ProfileFormat.GenderLabel(learner.Gender),
            "dob" => ProfileFormat.DisplayDob(learner.DateOfBirth),

            // A-272: a linked ID is never revealed, only its masked form.
            "id" => (await identities.FindAsync(who.Id, cancellationToken))?.Masked ?? string.Empty,
            _ => string.Empty,
        };
        return new RevealDto(value);
    }

    public async Task<ProfileEditDto> GetEditableAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        return new ProfileEditDto(
            Skills(learner),
            ProfileFormat.GenderKey(learner.Gender),
            ProfileFormat.IsoDate(learner.DateOfBirth),
            options.Value.ProfileSkillOptions(),
            ProfileFormat.DisplayPhone(learner.Phone),
            learner.Email ?? string.Empty);
    }

    public async Task<Result<ProfileSavedDto>> SaveAsync(LearnerRef who, ProfileEditRequest edit, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;

        DateOnly? dob = string.IsNullOrEmpty(edit.Dob)
            ? null
            : DateOnly.ParseExact(edit.Dob, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (dob > DateOnly.FromDateTime(now))
        {
            return LearnerErrors.DobInFuture;
        }

        var skills = edit.Skills
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(LearnerProfile.MaxProfileSkills)
            .ToList();
        var firstSkills = learner.EditDetails(JsonSerializer.Serialize(skills), skills.Count > 0, ProfileFormat.ParseGender(edit.Gender), dob, now);

        var earned = 0;
        if (firstSkills)
        {
            earned = Xp.ProfileSkills;
            outbox.Add(new ProfileTaskCompleted(who.Id, "skills") { TenantId = tenant.TenantId });
            learner.RecordAwardedXp(earned, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProfileSavedDto(await BuildAsync(learner, cancellationToken), earned);
    }

    public async Task<Result<ProfileSavedDto>> UploadPhotoAsync(LearnerRef who, UploadedFile? photo, CancellationToken cancellationToken)
    {
        var read = await FileSniffer.ReadAsync(photo, options.Value.MaxPhotoBytes, FileSniffer.Images, cancellationToken);
        if (!read.IsSuccess)
        {
            return read.Error;
        }

        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var previous = learner.PhotoBlob;
        var blobName = $"photos/{who.Id:N}/{Guid.NewGuid():N}";
        await using (read.Value.Content)
        {
            await blobs.UploadAsync(blobName, read.Value.Content, read.Value.ContentType, cancellationToken);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var first = learner.SetPhoto(blobName, now);
        var earned = 0;
        if (first)
        {
            earned = Xp.ProfilePhoto;
            outbox.Add(new ProfileTaskCompleted(who.Id, "photo") { TenantId = tenant.TenantId });
            learner.RecordAwardedXp(earned, now);
        }

        outbox.Add(new PhotoChanged(who.Id, PhotoUrl(learner)!) { TenantId = tenant.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Only after the new photo is saved, so a failed save never leaves the learner without one.
        if (previous is not null)
        {
            await blobs.DeleteAsync(previous, cancellationToken);
        }

        return new ProfileSavedDto(await BuildAsync(learner, cancellationToken), earned);
    }

    public async Task<Result<StoredBlob>> GetPhotoAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var learner = await learners.GetAsync(learnerId, cancellationToken);
        if (learner?.PhotoBlob is not { } blobName)
        {
            return LearnerErrors.PhotoNotFound;
        }

        var blob = await blobs.OpenReadAsync(blobName, cancellationToken);
        return blob is null ? LearnerErrors.PhotoNotFound : blob;
    }

    private static IReadOnlyList<string> Skills(LearnerProfile learner) =>
        JsonSerializer.Deserialize<List<string>>(learner.ProfileSkillsJson) ?? [];

    private async Task<PlayerProfileDto> BuildAsync(LearnerProfile learner, CancellationToken cancellationToken)
    {
        var linked = await identities.FindAsync(learner.Id, cancellationToken);
        var shown = progress.For(learner);
        var level = progress.LevelFor(shown.Xp);

        var done = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["contact"] = learner.Phone is not null || learner.Email is not null,
            ["name"] = learner.FirstName.Length > 0 && learner.LastName.Length > 0,
            ["photo"] = learner.HasPhoto,
            ["skills"] = learner.HasProfileSkills,
            ["drivers"] = learner.HasCareerDrivers,
        };
        var xp = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["photo"] = Xp.ProfilePhoto,
            ["skills"] = Xp.ProfileSkills,
            ["drivers"] = Xp.CareerDrivers,
        };
        var tasks = StrengthTasks
            .Select(t => new StrengthTaskDto(t.Id, t.Label, xp.GetValueOrDefault(t.Id), done[t.Id]))
            .ToList();
        var percent = StrengthTasks.Where(t => done[t.Id]).Sum(t => t.Weight);

        var fields = new List<MaskedFieldDto>
        {
            new("mobile", "Mobile number", ProfileFormat.MaskMobile(learner.Phone), learner.Phone is null),
            new("email", "Email", ProfileFormat.MaskEmail(learner.Email), learner.Email is null),
            new("gender", "Gender", learner.Gender is null ? ProfileFormat.NotAdded : "Hidden", learner.Gender is null),
            new("dob", "Date of birth", ProfileFormat.MaskDob(learner.DateOfBirth), learner.DateOfBirth is null),
        };
        if (linked is not null)
        {
            // Never revealable (A-272), so it is always "empty" to the eye button.
            fields.Add(new("id", "Government ID", $"{ProfileFormat.DocumentLabel(linked.Document)} {linked.Masked}", true));
        }

        return new PlayerProfileDto(
            ProfileFormat.PlayerId(learner.Id),
            learner.FullName,
            ProfileFormat.Initials(learner),
            level.Rank,
            learner.JoinedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            PhotoUrl(learner),
            level.Level,
            shown.Xp,
            level.Target,
            level.Start,
            BatchRank: null,
            BestSkill: null,
            learner.ArchetypeName is { } archetype ? new ArchetypeDto(archetype, learner.ArchetypeSummary ?? string.Empty) : null,
            new ProfileStatsDto(learner.StreakDays, shown.Badges, LearningHours: 0),
            new StrengthDto(percent, tasks),
            new ShowcaseDto([], null),
            fields,
            Skills(learner));
    }
}
