using Microsoft.EntityFrameworkCore;

namespace SkillMuni.Messaging.Erasure;

/// <summary>
/// A tombstone: the learner id and when the account was erased, nothing else (no tenant, no personal data). The id
/// is the account's global UUID, so it needs no tenant to be unique.
/// </summary>
public sealed class ErasedLearner
{
    private ErasedLearner()
    {
    }

    public ErasedLearner(Guid learnerId, DateTime erasedAt)
    {
        LearnerId = learnerId;
        ErasedAt = erasedAt;
    }

    public Guid LearnerId { get; private set; }

    public DateTime ErasedAt { get; private set; }
}

public static class ErasureModelBuilderExtensions
{
    /// <summary>Maps <c>erased_learners</c> (tombstones) into the calling service's own schema.</summary>
    public static ModelBuilder AddErasureTombstones(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ErasedLearner>(b =>
        {
            b.ToTable("erased_learners");
            b.HasKey(e => e.LearnerId);
            b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType("char(36)").ValueGeneratedNever();
            b.Property(e => e.ErasedAt).HasColumnName("erased_at");
        });
        return modelBuilder;
    }
}
