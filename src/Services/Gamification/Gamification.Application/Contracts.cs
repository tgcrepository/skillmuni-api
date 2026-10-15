namespace Gamification.Application;

// Shapes match arena.api.ts, stats.api.ts and profile.api.ts (FRONTEND_TYPES.md), serialised camelCase.

public sealed record ArenaPlayerDto(
    string Id,
    int Rank,
    string Name,
    string Initials,
    string? Photo,
    int Xp,
    int Streak,
    int ModulesDone,
    int Movement,
    bool You,
    string Title,
    int Level,
    int Badges,
    string TopSkill,
    int Cheers,
    bool Cheered);

public sealed record ArenaMeDto(int Rank, int Xp, int RankToday, int Streak, string? NextName, int? NextXp, int TopXp);

public sealed record RankUpDto(int To, string Passed);

public sealed record VoucherDto(string Discount, string Title, int Cost, int Ready);

public sealed record ArenaRewardsDto(int Coins, VoucherDto? Voucher);

public sealed record ArenaBoardDto(
    string Scope,
    string Period,
    IReadOnlyList<ArenaPlayerDto> Players,
    ArenaMeDto Me,
    IReadOnlyList<string> Week,
    int StreakBonus,
    int PromotionRank,
    string League,
    RankUpDto? RankUp,
    ArenaRewardsDto Rewards);

public sealed record CheerRequest(bool On);

public sealed record CheersDto(int Cheers);

public sealed record DailyQuestDto(string Id, string Icon, string Title, string ProgressLabel, double Percent, int Xp, bool Done, string Link);

public sealed record DailyQuestsDto(DateTime ResetsAt, IReadOnlyList<DailyQuestDto> Items);

public sealed record BadgeProgressDto(string Id, string Name, string Icon, string ProgressLabel, double Percent);

public sealed record ShowcaseSlotDto(string Id, string Name, string Icon, string Rarity);

public sealed record RewardsPlayerDto(string Name, string Initials, string Title, string? PhotoUrl, int Level, int Xp, int NextLevelXp, int? BatchRank);

public sealed record StreakDto(int Days, IReadOnlyList<string> Week);

public sealed record WalletDto(int Xp, int Coins, int CoinsEarned, int CoinsSpent, int Credits);

public sealed record RewardsBadgesDto(int Earned, int Total, IReadOnlyList<ShowcaseSlotDto?> Showcase, IReadOnlyList<BadgeProgressDto> UpNext);

public sealed record RankDto(int Rank, int GapXp, string NextName);

/// <summary>stats.api.ts RewardsDashboard (API-129).</summary>
public sealed record RewardsDashboardDto(
    RewardsPlayerDto Player,
    StreakDto Streak,
    WalletDto Wallet,
    DailyQuestsDto Quests,
    RewardsBadgesDto Badges,
    RankDto? Rank,
    double LearningHours);

public sealed record BadgeCtaDto(string Label, string Url);

public sealed record BadgeTiersDto(int Reached, int Total);

public sealed record BadgeDto(
    string Id,
    string Name,
    string Icon,
    string Category,
    string Rarity,
    string State,
    string? EarnedOn,
    string? ProgressLabel,
    double Percent,
    string HowTo,
    double OwnedPct,
    BadgeCtaDto? Cta,
    BadgeTiersDto? Tiers,
    bool Pinned);

/// <summary>stats.api.ts BadgeCatalogue (API-132/133).</summary>
public sealed record BadgeCatalogueDto(int Earned, int Total, IReadOnlyList<BadgeDto> Badges);

public sealed record ShowcaseRequest(IReadOnlyList<string> Ids);

/// <summary>MISS-01: one ledger line.</summary>
public sealed record XpEntryDto(string Reason, string Label, string SourceId, int Xp, DateTime OccurredAt);

/// <summary>MISS-04.</summary>
public sealed record StreakDetailDto(int Days, int Best, IReadOnlyList<string> Week, int Bonus);
