using SkillMuni.Common.Messaging;

namespace Identity.Application.Events;

// Events Identity publishes (BACKEND_HANDOFF "Key events" + the ones the claim and privacy flows need).
// Payloads carry ids and only the personal data the consumer needs to do its job.

/// <summary>A new learner account. Learner creates the profile; Gamification can start the ledger.</summary>
public sealed record LearnerRegistered(
    Guid AccountId,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string Market,
    string Source) : IntegrationEvent
{
    public const string Name = "identity.learner.registered";

    public override string EventType => Name;
}

/// <summary>A mobile or email verified with a code (A-273). Learner updates Quest 2 / the profile.</summary>
public sealed record ContactVerified(Guid AccountId, string Kind, string Value) : IntegrationEvent
{
    public const string Name = "identity.contact.verified";

    public override string EventType => Name;
}

/// <summary>V2: the learner chose what to share. Learner pre-fills quests 1–5 from the institute's CV.</summary>
public sealed record ClaimConsented(Guid ClaimId, Guid AccountId, bool ShareProgress, bool VerifyCertificate, bool ApplyForMe) : IntegrationEvent
{
    public const string Name = "identity.claim.consented";

    public override string EventType => Name;
}

/// <summary>V4: the learner confirmed the institute profile. Gamification credits the claim XP.</summary>
public sealed record ClaimConfirmed(Guid ClaimId, Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.claim.confirmed";

    public override string EventType => Name;
}

/// <summary>V5: CV deletes the file and everything read from it; the institute sees "Declined", never the reason.</summary>
public sealed record ClaimDeclined(Guid ClaimId) : IntegrationEvent
{
    public const string Name = "identity.claim.declined";

    public override string EventType => Name;
}

/// <summary>MISS-07: a consent given or withdrawn after sign-up.</summary>
public sealed record ConsentChanged(Guid AccountId, string Scope, string PolicyVersion, bool Granted) : IntegrationEvent
{
    public const string Name = "identity.consent.changed";

    public override string EventType => Name;
}

/// <summary>MISS-08: every service exports what it holds for this learner.</summary>
public sealed record DataExportRequested(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.data-export.requested";

    public override string EventType => Name;
}

/// <summary>MISS-08: every service erases what it holds for this learner.</summary>
public sealed record LearnerErased(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.learner.erased";

    public override string EventType => Name;
}
