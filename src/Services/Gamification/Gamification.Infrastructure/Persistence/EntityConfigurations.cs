using Gamification.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gamification.Infrastructure.Persistence;

// Explicit snake_case names and types keep the schema readable and the hand-checked migration deterministic.
// Generated from one spec together with the InitialCreate migration and the snapshot, so the three agree.
// PII columns: players.name and photo_url (shown on the Arena to the tenant's other learners).

internal static class Columns
{
    public const string Guid = "char(36)";
    public const int TenantLength = 64;

    public static void Keys<T>(EntityTypeBuilder<T> b)
        where T : Entity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasColumnType(Guid).ValueGeneratedNever();
        b.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(TenantLength).IsRequired();
    }
}

internal sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> b)
    {
        b.ToTable("players");
        Columns.Keys(b);
        b.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(e => e.Market).HasColumnName("market").HasMaxLength(2).IsRequired();
        b.Property(e => e.PhotoUrl).HasColumnName("photo_url").HasMaxLength(300);
        b.Property(e => e.Batch).HasColumnName("batch").HasMaxLength(40);
        b.Property(e => e.TotalXp).HasColumnName("total_xp");
        b.Property(e => e.Badges).HasColumnName("badges");
        b.Property(e => e.StreakDays).HasColumnName("streak_days");
        b.Property(e => e.BestStreak).HasColumnName("best_streak");
        b.Property(e => e.LastActiveDay).HasColumnName("last_active_day").HasColumnType("date");
        b.Property(e => e.LearningMinutes).HasColumnName("learning_minutes");
        b.Property(e => e.CheersReceived).HasColumnName("cheers_received");
        b.Property(e => e.LastSeenRank).HasColumnName("last_seen_rank");
        b.Property(e => e.ShowcaseJson).HasColumnName("showcase").HasColumnType("json").IsRequired();
        b.Property(e => e.CountersJson).HasColumnName("counters").HasColumnType("json").IsRequired();
        b.Property(e => e.JoinedAt).HasColumnName("joined_at");
        // Optimistic concurrency (players.version): two events for one learner handled at once must not overwrite each
        // other's counters and totals. The loser fails, is retried and re-reads the player.
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.Property(e => e.Version).HasColumnName("version").IsConcurrencyToken();

        b.HasIndex(e => new { e.TenantId, e.Batch }).HasDatabaseName("ix_players_batch");
    }
}

internal sealed class XpEntryConfiguration : IEntityTypeConfiguration<XpEntry>
{
    public void Configure(EntityTypeBuilder<XpEntry> b)
    {
        b.ToTable("xp_entries");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(60).IsRequired();
        b.Property(e => e.SourceId).HasColumnName("source_id").HasMaxLength(120).IsRequired();
        b.Property(e => e.Xp).HasColumnName("xp");
        b.Property(e => e.OccurredAt).HasColumnName("occurred_at");

        b.HasIndex(e => new { e.LearnerId, e.Reason, e.SourceId }).IsUnique().HasDatabaseName("ux_xp_entries_reason");
        b.HasIndex(e => new { e.TenantId, e.OccurredAt }).HasDatabaseName("ix_xp_entries_board");
        b.HasIndex(e => new { e.LearnerId, e.OccurredAt }).HasDatabaseName("ix_xp_entries_learner");
    }
}

internal sealed class BadgeAwardConfiguration : IEntityTypeConfiguration<BadgeAward>
{
    public void Configure(EntityTypeBuilder<BadgeAward> b)
    {
        b.ToTable("badge_awards");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.BadgeId).HasColumnName("badge_id").HasMaxLength(60).IsRequired();
        b.Property(e => e.EarnedAt).HasColumnName("earned_at");

        b.HasIndex(e => new { e.LearnerId, e.BadgeId }).IsUnique().HasDatabaseName("ux_badge_awards_learner_badge");
        b.HasIndex(e => new { e.TenantId, e.BadgeId }).HasDatabaseName("ix_badge_awards_badge");
    }
}

internal sealed class ActiveDayConfiguration : IEntityTypeConfiguration<ActiveDay>
{
    public void Configure(EntityTypeBuilder<ActiveDay> b)
    {
        b.ToTable("active_days");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Day).HasColumnName("day").HasColumnType("date");

        b.HasIndex(e => new { e.LearnerId, e.Day }).IsUnique().HasDatabaseName("ux_active_days_learner_day");
    }
}

internal sealed class DailyProgressConfiguration : IEntityTypeConfiguration<DailyProgress>
{
    public void Configure(EntityTypeBuilder<DailyProgress> b)
    {
        b.ToTable("daily_progress");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Day).HasColumnName("day").HasColumnType("date");
        b.Property(e => e.QuestId).HasColumnName("quest_id").HasMaxLength(40).IsRequired();
        b.Property(e => e.Value).HasColumnName("value");
        b.Property(e => e.Completed).HasColumnName("completed");

        b.HasIndex(e => new { e.LearnerId, e.Day, e.QuestId }).IsUnique().HasDatabaseName("ux_daily_progress_learner_day_quest");
    }
}

internal sealed class CheerConfiguration : IEntityTypeConfiguration<Cheer>
{
    public void Configure(EntityTypeBuilder<Cheer> b)
    {
        b.ToTable("cheers");
        Columns.Keys(b);
        b.Property(e => e.FromLearnerId).HasColumnName("from_learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.ToLearnerId).HasColumnName("to_learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Active).HasColumnName("active");
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(e => new { e.FromLearnerId, e.ToLearnerId }).IsUnique().HasDatabaseName("ux_cheers_from_to");
        b.HasIndex(e => e.ToLearnerId).HasDatabaseName("ix_cheers_to");
    }
}

internal sealed class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
{
    public void Configure(EntityTypeBuilder<Challenge> b)
    {
        b.ToTable("challenges");
        Columns.Keys(b);
        b.Property(e => e.FromLearnerId).HasColumnName("from_learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.ToLearnerId).HasColumnName("to_learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.EndsAt).HasColumnName("ends_at");

        b.HasIndex(e => new { e.FromLearnerId, e.ToLearnerId, e.EndsAt }).HasDatabaseName("ix_challenges_pair");
        b.HasIndex(e => e.ToLearnerId).HasDatabaseName("ix_challenges_to");
    }
}
