using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Muni.Domain;

namespace Muni.Infrastructure.Persistence;

// Explicit snake_case names and types keep the schema readable.
// PII columns: learners.first_name, learners.last_name; messages.text (whatever the learner typed).

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

internal sealed class MuniLearnerConfiguration : IEntityTypeConfiguration<MuniLearner>
{
    public void Configure(EntityTypeBuilder<MuniLearner> b)
    {
        b.ToTable("learners");
        Columns.Keys(b);
        b.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        b.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        b.Property(e => e.JoinedAt).HasColumnName("joined_at");
        b.Property(e => e.TotalXp).HasColumnName("total_xp");
        b.Property(e => e.Level).HasColumnName("level");
        b.Property(e => e.Rank).HasColumnName("rank_name").HasMaxLength(40).IsRequired();
        b.Property(e => e.NextLevelXp).HasColumnName("next_level_xp");
        b.Property(e => e.StreakDays).HasColumnName("streak_days");
        b.Property(e => e.QuestsDoneMask).HasColumnName("quests_done_mask");
        b.Property(e => e.Archetype).HasColumnName("archetype").HasMaxLength(100);
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.Property(e => e.ErasedAt).HasColumnName("erased_at");
        b.Property(e => e.Version).HasColumnName("version").IsConcurrencyToken();
        b.Ignore(e => e.QuestsDone);
    }
}

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.ToTable("messages");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.ConversationId).HasColumnName("conversation_id").HasColumnType(Columns.Guid);
        b.Property(e => e.From).HasColumnName("speaker").HasMaxLength(8).IsRequired();
        b.Property(e => e.Text).HasColumnName("text").HasMaxLength(ChatMessage.MaxTextLength).IsRequired();
        b.Property(e => e.Topic).HasColumnName("topic").HasMaxLength(40);
        b.Property(e => e.ReplyJson).HasColumnName("reply").HasColumnType("json");
        b.Property(e => e.CreatedAt).HasColumnName("created_at");

        // History and "continue the conversation" read the learner's newest lines; retention deletes by age.
        b.HasIndex(e => new { e.LearnerId, e.CreatedAt }).HasDatabaseName("ix_messages_learner_created");
        b.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_messages_created");
    }
}

internal sealed class ReplyRatingConfiguration : IEntityTypeConfiguration<ReplyRating>
{
    public void Configure(EntityTypeBuilder<ReplyRating> b)
    {
        b.ToTable("ratings");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.MessageRef).HasColumnName("message_ref").HasMaxLength(ReplyRating.MaxRefLength).IsRequired();
        b.Property(e => e.Helpful).HasColumnName("helpful");
        b.Property(e => e.RatedAt).HasColumnName("rated_at");

        b.HasIndex(e => new { e.LearnerId, e.MessageRef }).IsUnique().HasDatabaseName("ux_ratings_learner_message");
        b.HasIndex(e => e.RatedAt).HasDatabaseName("ix_ratings_rated");
    }
}
