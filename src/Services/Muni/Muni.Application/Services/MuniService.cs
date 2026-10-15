using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Muni.Domain;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Muni.Application.Services;

/// <summary>API-110–114: Muni chat (Figma Muni Chat v2). Text only: Muni has no voice (BRD §3.20, A-314).</summary>
public interface IMuniService
{
    Task<MuniWelcomeDto> WelcomeAsync(LearnerRef who, CancellationToken cancellationToken);

    Task<MuniReplyDto> AskAsync(LearnerRef who, AskRequest request, CancellationToken cancellationToken);

    Task<Result<RevealDto>> RevealAsync(LearnerRef who, string field, CancellationToken cancellationToken);

    Task<Result> RateAsync(LearnerRef who, string messageId, bool? helpful, CancellationToken cancellationToken);

    Task<IReadOnlyList<MuniHistoryItemDto>> HistoryAsync(LearnerRef who, CancellationToken cancellationToken);
}

public sealed class MuniService(
    ILearnerSnapshotBuilder snapshots,
    IMuniResponder responder,
    IPrivateFieldClient privateFields,
    IChatRepository chats,
    IRatingRepository ratings,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<MuniOptions> options) : IMuniService
{
    public const int MaxTitleLength = 60;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<MuniTopicDto> Topics =
    [
        new("jobs", "briefcase", "Jobs", 5),
        new("learn", "play", "Learn", 4),
        new("assess", "target", "Assess", 2),
        new("me", "user", "Me", 4),
    ];

    private MuniOptions Settings => options.Value;

    public async Task<MuniWelcomeDto> WelcomeAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var me = await snapshots.BuildAsync(who, cancellationToken);
        return new MuniWelcomeDto(
            me.First,
            $"Level {me.Level} · {me.Rank}, {me.Xp} XP. Ask me anything about Skillmuni, or start with what I’d do right now:",
            Suggestions(me),
            Topics,
            Unread: 0,
            Nudge: null);
    }

    public async Task<MuniReplyDto> AskAsync(LearnerRef who, AskRequest request, CancellationToken cancellationToken)
    {
        var text = string.IsNullOrWhiteSpace(request.Text) ? request.Intent!.Trim() : request.Text.Trim();
        var me = await snapshots.BuildAsync(who, cancellationToken);
        var answer = await responder.AnswerAsync(new MuniQuestion(text, request.Intent), me, cancellationToken);
        var chips = answer.Chips.Take(3).ToList();
        if (me.Erased)
        {
            // A token issued before the erasure is still valid for a few minutes: answer, but keep nothing.
            return new MuniReplyDto(Guid.NewGuid().ToString(), answer.Blocks, chips);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var last = await chats.LastAsync(who.Id, cancellationToken);
        var conversation = last is not null && now - last.CreatedAt <= Settings.ConversationGap ? last.ConversationId : Guid.NewGuid();

        chats.Add(ChatMessage.FromLearner(tenant.TenantId, who.Id, conversation, text, now));
        var summary = answer.Blocks.OfType<TextBlock>().FirstOrDefault()?.Text ?? string.Empty;
        var replyJson = JsonSerializer.Serialize(new { blocks = answer.Blocks, chips }, Json);

        // A tick later, so the reply always sorts after the question.
        var reply = ChatMessage.FromMuni(tenant.TenantId, who.Id, conversation, summary, answer.Topic, replyJson, now.AddTicks(10));
        chats.Add(reply);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MuniReplyDto(reply.Id.ToString(), answer.Blocks, chips);
    }

    public async Task<Result<RevealDto>> RevealAsync(LearnerRef who, string field, CancellationToken cancellationToken)
    {
        var value = await privateFields.RevealAsync(field, cancellationToken);
        return value is null ? MuniErrors.RevealUnavailable : new RevealDto(value);
    }

    public async Task<Result> RateAsync(LearnerRef who, string messageId, bool? helpful, CancellationToken cancellationToken)
    {
        var reference = messageId.Trim();
        if (reference.Length is 0 or > ReplyRating.MaxRefLength)
        {
            return MuniErrors.InvalidMessageId;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await ratings.FindAsync(who.Id, reference, cancellationToken);
        if (helpful is null)
        {
            if (existing is null)
            {
                return Result.Success();
            }

            ratings.Remove(existing);
        }
        else if (existing is null)
        {
            ratings.Add(ReplyRating.Rate(tenant.TenantId, who.Id, reference, helpful.Value, now));
        }
        else
        {
            existing.Change(helpful.Value, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<MuniHistoryItemDto>> HistoryAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var lines = await chats.RecentAsync(who.Id, now.AddDays(-Settings.HistoryDays), Settings.HistoryLines, cancellationToken);
        var zone = Settings.TimeZoneFor(who.Market);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, zone));

        return lines
            .GroupBy(l => l.ConversationId)
            .Select(g => g.OrderBy(l => l.CreatedAt).ToList())
            .OrderByDescending(c => c[^1].CreatedAt)
            .Take(Settings.HistoryConversations)
            .Select(c => new MuniHistoryItemDto(
                c[0].ConversationId.ToString(),
                Title(c),
                When(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(c[^1].CreatedAt, zone)), today),
                c.Select(l => new HistoryLineDto(l.From, l.Text)).ToList()))
            .ToList();
    }

    /// <summary>Today · Yesterday · Mon (this week) · 12 Oct.</summary>
    internal static string When(DateOnly day, DateOnly today)
    {
        var daysAgo = today.DayNumber - day.DayNumber;
        return daysAgo switch
        {
            <= 0 => "Today",
            1 => "Yesterday",
            < 7 => day.ToString("ddd", CultureInfo.InvariantCulture),
            _ => day.ToString("d MMM", CultureInfo.InvariantCulture),
        };
    }

    internal static string Title(IReadOnlyList<ChatLine> conversation)
    {
        var first = conversation.FirstOrDefault(l => l.From == Speakers.Learner)?.Text ?? conversation[0].Text;
        return first.Length <= MaxTitleLength ? first : $"{first[..(MaxTitleLength - 1)].TrimEnd()}…";
    }

    /// <summary>
    /// "For you right now". New learners (no Career Drivers yet) start with discovery; others with Brain-Vita
    /// (resume point, B-44), jobs and their streak. No made-up numbers: Learning and Careers aren't asked yet.
    /// </summary>
    internal static IReadOnlyList<MuniSuggestionDto> Suggestions(LearnerSnapshot me) => me.Fresh
        ?
        [
            new("drivers", "compass", "accent", "Discover your Career Drivers", "36 quick picks · 6 min", "Start", Link: "/assessments/career"),
            new("learn", "play", "success", "Start your first learning module", "2-minute brief, then a quick quiz", "Let’s go", Link: "/learn/brain-vita"),
            new("jobs", "briefcase", "brand", "See jobs that fit you", "Matched to your quests", "See jobs", Intent: "Jobs for me"),
        ]
        :
        [
            new("finish", "play", "success", "Finish Brain-Vita", "Pick up where you left off", "Let’s go", Link: "/learn/brain-vita"),
            new("jobs", "briefcase", "accent", "See jobs that fit you", "Matched to your profile", "See jobs", Intent: "Jobs for me"),
            me.Streak > 0
                ? new MuniSuggestionDto("streak", "flame", "brand", $"Keep your {me.Streak}-day streak", "One brief’s quiz today keeps it alive", "Quick quiz", Link: "/learn/brain-vita")
                : new MuniSuggestionDto("streak", "flame", "brand", "Start a streak today", "One brief’s quiz a day keeps it going", "Quick quiz", Link: "/learn/brain-vita"),
        ];
}
