using Microsoft.EntityFrameworkCore;

namespace SkillMuni.Messaging.Inbox;

/// <summary>
/// One row per event a service has handled, keyed by the event id. Written in the same transaction as the
/// handler's changes, so an event redelivered by Service Bus is skipped.
/// </summary>
public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    public InboxMessage(Guid id, string eventType, DateTime processedAt)
    {
        Id = id;
        EventType = eventType;
        ProcessedAt = processedAt;
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public DateTime ProcessedAt { get; private set; }
}

public static class InboxModelBuilderExtensions
{
    public static ModelBuilder AddInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox_messages");
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
            b.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
            b.Property(m => m.ProcessedAt).HasColumnName("processed_at");
        });
        return modelBuilder;
    }
}
