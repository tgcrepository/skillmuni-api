using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain;

namespace Platform.Infrastructure.Persistence;

// Explicit snake_case names and types keep the schema readable.
// PII: none stored in clear. notification_deliveries.recipient is masked (***3210, y***@gmail.com); codes are never stored.

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

internal sealed class TenantThemeConfiguration : IEntityTypeConfiguration<TenantTheme>
{
    /// <summary>The two default tenants (TenancyOptions): Skillmuni's own look, so the app keeps its built-in ramps.</summary>
    private static readonly DateTime Seeded = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<TenantTheme> b)
    {
        b.ToTable("tenant_themes");
        Columns.Keys(b);
        b.Property(e => e.Name).HasColumnName("name").HasMaxLength(TenantTheme.MaxNameLength).IsRequired();
        b.Property(e => e.LogoUrl).HasColumnName("logo_url").HasMaxLength(TenantTheme.MaxLogoUrlLength);
        b.Property(e => e.BrandJson).HasColumnName("brand").HasColumnType("json").IsRequired();
        b.Property(e => e.AccentJson).HasColumnName("accent").HasColumnType("json").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(e => e.TenantId).IsUnique().HasDatabaseName("ux_tenant_themes_tenant");

        b.HasData(
            new { Id = new Guid("5b1f0c2e-7d3a-4e8b-9c61-0a2f4d8e1b01"), TenantId = "skillmuni-in", Name = "Skillmuni", BrandJson = "{}", AccentJson = "{}", UpdatedAt = Seeded },
            new { Id = new Guid("5b1f0c2e-7d3a-4e8b-9c61-0a2f4d8e1b02"), TenantId = "skillmuni-za", Name = "Skillmuni", BrandJson = "{}", AccentJson = "{}", UpdatedAt = Seeded });
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Kind).HasColumnName("kind").HasMaxLength(40).IsRequired();
        b.Property(e => e.Title).HasColumnName("title").HasMaxLength(Notification.MaxTitleLength).IsRequired();
        b.Property(e => e.Body).HasColumnName("body").HasMaxLength(Notification.MaxBodyLength).IsRequired();
        b.Property(e => e.Link).HasColumnName("link").HasMaxLength(300);
        b.Property(e => e.SourceKey).HasColumnName("source_key").HasMaxLength(120).IsRequired();
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.ReadAt).HasColumnName("read_at");
        b.Ignore(e => e.IsRead);

        b.HasIndex(e => new { e.LearnerId, e.SourceKey }).IsUnique().HasDatabaseName("ux_notifications_learner_source");
        b.HasIndex(e => new { e.LearnerId, e.CreatedAt }).HasDatabaseName("ix_notifications_learner_created");
        b.HasIndex(e => new { e.LearnerId, e.ReadAt }).HasDatabaseName("ix_notifications_learner_read");
    }
}

internal sealed class NotificationPreferencesConfiguration : IEntityTypeConfiguration<NotificationPreferences>
{
    public void Configure(EntityTypeBuilder<NotificationPreferences> b)
    {
        b.ToTable("notification_preferences");
        Columns.Keys(b);
        b.Property(e => e.Push).HasColumnName("push");
        b.Property(e => e.WhatsApp).HasColumnName("whatsapp");
        b.Property(e => e.Sms).HasColumnName("sms");
        b.Property(e => e.Email).HasColumnName("email");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

internal sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> b)
    {
        b.ToTable("notification_deliveries");
        Columns.Keys(b);
        b.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20).IsRequired();
        b.Property(e => e.Template).HasColumnName("template").HasMaxLength(60).IsRequired();
        b.Property(e => e.Recipient).HasColumnName("recipient").HasMaxLength(120).IsRequired();
        b.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        b.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(40).IsRequired();
        b.Property(e => e.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(128);
        b.Property(e => e.QueueMessageId).HasColumnName("queue_message_id").HasMaxLength(128);
        b.Property(e => e.CreatedAt).HasColumnName("created_at");

        b.HasIndex(e => e.QueueMessageId).IsUnique().HasDatabaseName("ux_notification_deliveries_queue_message");
        b.HasIndex(e => new { e.TenantId, e.CreatedAt }).HasDatabaseName("ix_notification_deliveries_created");
    }
}
