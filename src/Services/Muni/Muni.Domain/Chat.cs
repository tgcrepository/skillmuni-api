namespace Muni.Domain;

/// <summary>Who said a chat line.</summary>
public static class Speakers
{
    public const string Learner = "me";
    public const string Muni = "muni";
}

/// <summary>
/// One chat line, kept for the history screen. Lines less than the conversation gap apart share a
/// <see cref="ConversationId"/>. Deleted after <c>Muni:HistoryDays</c> (BRD §9: retention still open).
/// </summary>
public sealed class ChatMessage : Entity
{
    public const int MaxTextLength = 1000;

    private ChatMessage()
    {
    }

    public Guid LearnerId { get; private set; }

    public Guid ConversationId { get; private set; }

    /// <summary><see cref="Speakers.Learner"/> or <see cref="Speakers.Muni"/>.</summary>
    public string From { get; private set; } = string.Empty;

    /// <summary>What the learner typed, or Muni's first text bubble.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Muni only: the topic the responder chose (for tuning the catalogue).</summary>
    public string? Topic { get; private set; }

    /// <summary>Muni only: the full reply (blocks and chips) as JSON.</summary>
    public string? ReplyJson { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static ChatMessage FromLearner(string tenantId, Guid learnerId, Guid conversationId, string text, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        ConversationId = conversationId,
        From = Speakers.Learner,
        Text = Clip(text),
        CreatedAt = now,
    };

    public static ChatMessage FromMuni(string tenantId, Guid learnerId, Guid conversationId, string text, string topic, string replyJson, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        ConversationId = conversationId,
        From = Speakers.Muni,
        Text = Clip(text),
        Topic = topic,
        ReplyJson = replyJson,
        CreatedAt = now,
    };

    private static string Clip(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= MaxTextLength ? trimmed : trimmed[..MaxTextLength];
    }
}

/// <summary>
/// Thumbs up / down on a Muni reply. <see cref="MessageRef"/> is the id the app sends: the reply's <c>id</c>, or
/// an id the app made up locally (muni.api.ts has no id on replies), so it is kept as text.
/// </summary>
public sealed class ReplyRating : Entity
{
    public const int MaxRefLength = 64;

    private ReplyRating()
    {
    }

    public Guid LearnerId { get; private set; }

    public string MessageRef { get; private set; } = string.Empty;

    public bool Helpful { get; private set; }

    public DateTime RatedAt { get; private set; }

    public static ReplyRating Rate(string tenantId, Guid learnerId, string messageRef, bool helpful, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        MessageRef = messageRef,
        Helpful = helpful,
        RatedAt = now,
    };

    public void Change(bool helpful, DateTime now)
    {
        Helpful = helpful;
        RatedAt = now;
    }
}
