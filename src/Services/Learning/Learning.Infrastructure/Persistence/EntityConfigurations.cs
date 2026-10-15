using Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learning.Infrastructure.Persistence;

// Explicit snake_case names and types keep the schema readable. No PII here: rows hold the learner id, content ids,
// scores and timestamps only.

internal static class Columns
{
    public const string Guid = "char(36)";
    public const int TenantLength = 64;
    public const int SlugLength = 80;
    public const int BriefIdLength = 200;

    public static void Keys<T>(EntityTypeBuilder<T> b)
        where T : Entity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasColumnType(Guid).ValueGeneratedNever();
        b.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(TenantLength).IsRequired();
    }
}

internal sealed class BriefProgressConfiguration : IEntityTypeConfiguration<BriefProgress>
{
    public void Configure(EntityTypeBuilder<BriefProgress> b)
    {
        b.ToTable("brief_progress");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.BriefId).HasColumnName("brief_id").HasMaxLength(Columns.BriefIdLength).IsRequired();
        b.Property(e => e.ModuleSlug).HasColumnName("module_slug").HasMaxLength(Columns.SlugLength).IsRequired();
        b.Property(e => e.TopicSlug).HasColumnName("topic_slug").HasMaxLength(Columns.SlugLength).IsRequired();
        b.Property(e => e.BriefIndex).HasColumnName("brief_index");
        b.Property(e => e.Bookmarked).HasColumnName("bookmarked");
        b.Property(e => e.Helpful).HasColumnName("helpful");
        b.Property(e => e.PassedAt).HasColumnName("passed_at");
        b.Property(e => e.Xp).HasColumnName("xp");
        b.Property(e => e.LastCorrect).HasColumnName("last_correct");
        b.Property(e => e.LastTotal).HasColumnName("last_total");
        b.Property(e => e.LastSubmittedAt).HasColumnName("last_submitted_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.Ignore(e => e.Passed);

        b.HasIndex(e => new { e.LearnerId, e.BriefId }).IsUnique().HasDatabaseName("ux_brief_progress_learner_brief");
        b.HasIndex(e => new { e.LearnerId, e.ModuleSlug }).HasDatabaseName("ix_brief_progress_learner_module");
        b.HasIndex(e => new { e.LearnerId, e.PassedAt }).HasDatabaseName("ix_brief_progress_learner_passed");
    }
}

internal sealed class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> b)
    {
        b.ToTable("quiz_attempts");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.QuizId).HasColumnName("quiz_id").HasMaxLength(Columns.BriefIdLength).IsRequired();
        b.Property(e => e.Correct).HasColumnName("correct");
        b.Property(e => e.Total).HasColumnName("total");
        b.Property(e => e.Xp).HasColumnName("xp");
        b.Property(e => e.FirstAttempt).HasColumnName("first_attempt");
        b.Property(e => e.SubmittedAt).HasColumnName("submitted_at");

        b.HasIndex(e => new { e.LearnerId, e.QuizId }).HasDatabaseName("ix_quiz_attempts_learner_quiz");
    }
}

internal sealed class QuizDraftConfiguration : IEntityTypeConfiguration<QuizDraft>
{
    public void Configure(EntityTypeBuilder<QuizDraft> b)
    {
        b.ToTable("quiz_drafts");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.QuizId).HasColumnName("quiz_id").HasMaxLength(Columns.BriefIdLength).IsRequired();
        b.Property(e => e.AnswersJson).HasColumnName("answers").HasColumnType("json").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(e => new { e.LearnerId, e.QuizId }).IsUnique().HasDatabaseName("ux_quiz_drafts_learner_quiz");
    }
}

internal sealed class LobbyStateConfiguration : IEntityTypeConfiguration<LobbyState>
{
    public void Configure(EntityTypeBuilder<LobbyState> b)
    {
        b.ToTable("lobby_states");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.AssessmentDoneAt).HasColumnName("assessment_done_at");
        b.Property(e => e.JobsVisitedAt).HasColumnName("jobs_visited_at");
        b.Property(e => e.FirstMovesClaimedAt).HasColumnName("first_moves_claimed_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.Ignore(e => e.Claimed);

        b.HasIndex(e => e.LearnerId).IsUnique().HasDatabaseName("ux_lobby_states_learner");
    }
}

internal sealed class ActivityDayConfiguration : IEntityTypeConfiguration<ActivityDay>
{
    public void Configure(EntityTypeBuilder<ActivityDay> b)
    {
        b.ToTable("activity_days");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Day).HasColumnName("day").HasColumnType("date");
        b.Property(e => e.Minutes).HasColumnName("minutes");

        b.HasIndex(e => new { e.LearnerId, e.Day }).IsUnique().HasDatabaseName("ux_activity_days_learner_day");
    }
}
