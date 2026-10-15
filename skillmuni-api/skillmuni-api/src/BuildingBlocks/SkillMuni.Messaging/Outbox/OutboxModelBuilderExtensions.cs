using Microsoft.EntityFrameworkCore;

namespace SkillMuni.Messaging.Outbox;

public static class OutboxModelBuilderExtensions
{
    /// <summary>Maps <c>outbox_messages</c> into the calling service's own schema.</summary>
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
            b.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
            b.Property(m => m.Payload).HasColumnName("payload").IsRequired();
            b.Property(m => m.TenantId).HasColumnName("tenant_id").HasMaxLength(64).IsRequired();
            b.Property(m => m.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
            b.Property(m => m.OccurredAt).HasColumnName("occurred_at");
            b.Property(m => m.ProcessedAt).HasColumnName("processed_at");
            b.Property(m => m.Attempts).HasColumnName("attempts");
            b.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(1000);
            b.HasIndex(m => new { m.ProcessedAt, m.OccurredAt });
        });
        return modelBuilder;
    }
}
