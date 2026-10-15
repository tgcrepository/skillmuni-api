using System.Globalization;
using Assessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Assessment.Infrastructure.Persistence;

// Explicit snake_case names and types keep the schema readable.
// PII: none beyond the learner id. Answers, scores and archetypes are personal data about motivation, erased with the
// account (identity.learner.erased).

internal static class Columns
{
    public const string Guid = "char(36)";
    public const int TenantLength = 64;

    /// <summary>Room for the 36 answer codes and a larger survey later.</summary>
    public const int AnswersLength = 100;

    public static void Keys<T>(EntityTypeBuilder<T> b)
        where T : Entity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasColumnType(Guid).ValueGeneratedNever();
        b.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(TenantLength).IsRequired();
    }
}

internal sealed class CareerJourneyConfiguration : IEntityTypeConfiguration<CareerJourney>
{
    public void Configure(EntityTypeBuilder<CareerJourney> b)
    {
        b.ToTable("journeys");
        Columns.Keys(b);
        b.Ignore(e => e.LearnerId);
        b.Ignore(e => e.EarnsXp);
        b.Ignore(e => e.AttemptInProgress);
        b.Property(e => e.AnswersCode).HasColumnName("answers").HasMaxLength(Columns.AnswersLength).IsRequired();
        b.Property(e => e.Current).HasColumnName("current_pair");
        b.Property(e => e.Xp).HasColumnName("xp");
        b.Property(e => e.IslandsCleared).HasColumnName("islands_cleared");
        b.Property(e => e.Remind).HasColumnName("remind");
        b.Property(e => e.RemindChangedAt).HasColumnName("remind_changed_at");
        b.Property(e => e.Attempts).HasColumnName("attempts");
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // The Platform reminder job will look for learners who asked for a nudge and haven't finished.
        b.HasIndex(e => new { e.TenantId, e.Remind, e.Attempts }).HasDatabaseName("ix_journeys_remind");
    }
}

internal sealed class CareerAttemptConfiguration : IEntityTypeConfiguration<CareerAttempt>
{
    public void Configure(EntityTypeBuilder<CareerAttempt> b)
    {
        b.ToTable("attempts");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Number).HasColumnName("number");
        b.Property(e => e.TakenOn).HasColumnName("taken_on");
        b.Property(e => e.Scores)
            .HasColumnName("scores")
            .HasMaxLength(64)
            .IsRequired()
            .HasConversion(ScoresConversion.Converter, ScoresConversion.Comparer);
        b.Property(e => e.ArchetypeId).HasColumnName("archetype_id").HasMaxLength(40).IsRequired();
        b.Property(e => e.XpEarned).HasColumnName("xp_earned");
        b.Property(e => e.AnswersCode).HasColumnName("answers").HasMaxLength(Columns.AnswersLength).IsRequired();
        b.Property(e => e.SubmittedAt).HasColumnName("submitted_at");

        b.HasIndex(e => new { e.LearnerId, e.Number }).IsUnique().HasDatabaseName("ux_attempts_learner_number");
    }
}

/// <summary>Scores A–I as a comma-separated list ("10,10,12,9,14,12,14,13,14"): compact and readable in SQL.</summary>
internal static class ScoresConversion
{
    public static readonly ValueConverter<DriverScores, string> Converter = new(
        v => string.Join(',', v.Values.Select(x => x.ToString(CultureInfo.InvariantCulture))),
        v => new DriverScores(v.Split(',', StringSplitOptions.None).Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToList()));

    public static readonly ValueComparer<DriverScores> Comparer = new(
        (a, b) => a == null ? b == null : a.Equals(b),
        v => v.GetHashCode(),
        v => new DriverScores(v.Values));
}
