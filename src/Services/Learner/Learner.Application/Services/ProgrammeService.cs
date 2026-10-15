using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Errors;
using Learner.Application.Events;
using Learner.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Services;

/// <summary>API-122–124: the programme-code bonus quest (Figma section 13), plus the tenant admin's code list.</summary>
public interface IProgrammeService
{
    /// <summary>404 for an unknown code; after 5 wrong codes, 429 for 30 minutes (Figma P4).</summary>
    Task<Result<ProgrammeDto>> CheckAsync(LearnerRef who, string code, CancellationToken cancellationToken);

    Task<Result<BankBranchDto>> FindIfscAsync(string ifsc, CancellationToken cancellationToken);

    Task<Result<ProgrammeResultDto>> SubmitAsync(LearnerRef who, ProgrammeSubmission submission, CancellationToken cancellationToken);

    Task<ProgrammeDto> SaveAsync(SaveProgrammeRequest request, CancellationToken cancellationToken);
}

public sealed partial class ProgrammeService(
    ILearnerAccessor accessor,
    IProgrammeRepository programmes,
    IEnrolmentRepository enrolments,
    IBankDirectory banks,
    IFieldEncryptor encryptor,
    IProgressService progress,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<LearnerServiceOptions> options,
    ILogger<ProgrammeService> logger) : IProgrammeService
{
    private const string India = "IN";

    public async Task<Result<ProgrammeDto>> CheckAsync(LearnerRef who, string code, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (learner.IsProgrammeLocked(now))
        {
            return LearnerErrors.TooManyTries;
        }

        var programme = await programmes.FindAsync(Normalize(code), cancellationToken);
        if (programme is null)
        {
            learner.RecordWrongProgrammeCode(now, options.Value.ProgrammeTries, options.Value.ProgrammeCoolDown);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return LearnerErrors.ProgrammeNotFound;
        }

        if (learner.ProgrammeWrongTries > 0)
        {
            learner.ResetProgrammeTries();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToDto(programme);
    }

    public async Task<Result<BankBranchDto>> FindIfscAsync(string ifsc, CancellationToken cancellationToken)
    {
        var code = Normalize(ifsc);
        if (!IfscPattern().IsMatch(code))
        {
            return LearnerErrors.InvalidIfsc;
        }

        var found = await banks.FindIfscAsync(code, cancellationToken);
        return found.Status switch
        {
            LookupStatus.Found when found.Value is { } branch => new BankBranchDto(branch.Bank, branch.Branch),
            LookupStatus.NotFound => LearnerErrors.IfscNotFound,
            _ => LearnerErrors.BankLookupUnavailable,
        };
    }

    public async Task<Result<ProgrammeResultDto>> SubmitAsync(LearnerRef who, ProgrammeSubmission submission, CancellationToken cancellationToken)
    {
        var programme = await programmes.FindAsync(Normalize(submission.Code), cancellationToken);
        if (programme is null)
        {
            return LearnerErrors.ProgrammeNotFound;
        }

        var learner = await accessor.GetOrCreateAsync(who, cancellationToken);
        if (await enrolments.ExistsAsync(who.Id, programme.Code, cancellationToken))
        {
            return LearnerErrors.AlreadyEnrolled;
        }

        var english = int.Parse(submission.English, CultureInfo.InvariantCulture);
        var maths = int.Parse(submission.Maths, CultureInfo.InvariantCulture);
        if (MeetsMarks(programme, submission.MarksFrom, english, maths) is false)
        {
            return LearnerErrors.MarksTooLow;
        }

        var bank = submission.Bank;
        if (programme.PaysStipend && bank is null)
        {
            return LearnerErrors.BankRequired;
        }

        if (bank is not null && learner.Market == India && !IfscPattern().IsMatch(Normalize(bank.Routing)))
        {
            return LearnerErrors.InvalidIfsc;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var enrolment = Enrolment.Submit(
            tenant.TenantId,
            who.Id,
            programme.Code,
            submission.MarksFrom,
            english,
            maths,
            JsonSerializer.Serialize(submission.Proficiency ?? new Dictionary<string, string>()),
            now);
        if (bank is not null)
        {
            enrolment.SetBank(
                bank.Holder.Trim(),
                bank.Type,
                encryptor.Encrypt(bank.Account),
                bank.Account[^4..],
                Normalize(bank.Routing),
                string.IsNullOrWhiteSpace(bank.Upi) ? null : bank.Upi.Trim());
        }

        enrolments.Add(enrolment);
        var earned = options.Value.Xp.Programme;
        learner.RecordAwardedXp(earned, now);
        outbox.Add(new ProgrammeEnrolled(who.Id, programme.Code, programme.PaysStipend) { TenantId = tenant.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogEnrolled(logger, who.Id, programme.Id);

        return new ProgrammeResultDto(earned, progress.For(learner), bank is null ? string.Empty : bank.Account[^2..]);
    }

    public async Task<ProgrammeDto> SaveAsync(SaveProgrammeRequest request, CancellationToken cancellationToken)
    {
        var code = Normalize(request.Code);
        var programme = await programmes.FindAsync(code, cancellationToken);
        if (programme is null)
        {
            programme = Programme.Create(tenant.TenantId, code);
            programmes.Add(programme);
        }

        programme.Describe(
            request.Name.Trim(),
            request.Partner.Trim(),
            request.Centre.Trim(),
            request.BatchStarts,
            request.Duration.Trim(),
            request.Stipend?.Trim() ?? string.Empty,
            request.MarksFrom,
            request.MinEnglish,
            request.MinMaths,
            request.Eligibility.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(programme);
    }

    /// <summary>A Class 12 rule isn't met by Class 10 marks; otherwise both minimums must be reached.</summary>
    internal static bool MeetsMarks(Programme programme, string marksFrom, int english, int maths) =>
        string.CompareOrdinal(marksFrom, programme.MarksFrom) >= 0 && english >= programme.MinEnglish && maths >= programme.MinMaths;

    internal static ProgrammeDto ToDto(Programme p) => new(
        p.Code,
        p.Name,
        p.Partner,
        p.Centre,
        p.BatchStarts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        p.Duration,
        p.Stipend,
        p.MarksFrom,
        p.MinEnglish,
        p.MinMaths,
        p.Eligibility);

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();

    [GeneratedRegex("^[A-Z]{4}0[A-Z0-9]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex IfscPattern();

    /// <summary>Logs the programme's id, never its code (the code unlocks the programme).</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Programme enrolled {LearnerId} {ProgrammeId}")]
    private static partial void LogEnrolled(ILogger logger, Guid learnerId, Guid programmeId);
}
