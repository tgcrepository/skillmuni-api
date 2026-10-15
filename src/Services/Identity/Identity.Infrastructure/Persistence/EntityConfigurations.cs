using Identity.Domain.Accounts;
using Identity.Domain.Claims;
using Identity.Domain.Codes;
using Identity.Domain.Common;
using Identity.Domain.Consents;
using Identity.Domain.Registration;
using Identity.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence;

// Explicit snake_case names and types keep the schema readable and the hand-checked migration deterministic.
// PII columns: accounts.first_name/last_name/email/phone, pending_registrations.*, claim_invites.learner_name/phone,
// sessions.user_agent, verification_codes.target.

internal static class Columns
{
    public const string Guid = "char(36)";
    public const int TenantLength = 64;
    public const int HashLength = 64;

    public static void Keys<T>(EntityTypeBuilder<T> b)
        where T : Entity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasColumnType(Guid).ValueGeneratedNever();
        b.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(TenantLength).IsRequired();
    }
}

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("accounts");
        Columns.Keys(b);
        b.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        b.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        b.Property(e => e.Email).HasColumnName("email").HasMaxLength(254);
        b.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
        b.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(500);
        b.Property(e => e.GoogleSubject).HasColumnName("google_subject").HasMaxLength(255);
        b.Property(e => e.Status).HasColumnName("status");
        b.Property(e => e.PhotoUrl).HasColumnName("photo_url").HasMaxLength(300);
        b.Property(e => e.Market).HasColumnName("market").HasMaxLength(2).IsRequired();
        b.Property(e => e.Roles).HasColumnName("roles").HasMaxLength(200).IsRequired();
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.Property(e => e.LastLoginAt).HasColumnName("last_login_at");

        b.HasIndex(e => new { e.TenantId, e.Email }).IsUnique().HasDatabaseName("ux_accounts_tenant_email");
        b.HasIndex(e => new { e.TenantId, e.Phone }).IsUnique().HasDatabaseName("ux_accounts_tenant_phone");
        b.HasIndex(e => new { e.TenantId, e.GoogleSubject }).IsUnique().HasDatabaseName("ux_accounts_tenant_google");
    }
}

internal sealed class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> b)
    {
        b.ToTable("verification_codes");
        Columns.Keys(b);
        b.Property(e => e.Purpose).HasColumnName("purpose");
        b.Property(e => e.Target).HasColumnName("target").HasMaxLength(300).IsRequired();
        b.Property(e => e.CodeHash).HasColumnName("code_hash").HasMaxLength(Columns.HashLength).IsRequired();
        b.Property(e => e.AccountId).HasColumnName("account_id").HasColumnType(Columns.Guid);
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        b.Property(e => e.ResendAt).HasColumnName("resend_at");
        b.Property(e => e.TriesLeft).HasColumnName("tries_left");
        b.Property(e => e.ConsumedAt).HasColumnName("consumed_at");

        b.HasIndex(e => new { e.TenantId, e.Purpose, e.Target, e.CreatedAt }).HasDatabaseName("ix_codes_target");
        b.HasIndex(e => new { e.Purpose, e.CodeHash }).HasDatabaseName("ix_codes_hash");
    }
}

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        b.ToTable("sessions");
        Columns.Keys(b);
        b.Property(e => e.AccountId).HasColumnName("account_id").HasColumnType(Columns.Guid);
        b.Property(e => e.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(Columns.HashLength).IsRequired();
        b.Property(e => e.PreviousRefreshTokenHash).HasColumnName("previous_refresh_token_hash").HasMaxLength(Columns.HashLength);
        b.Property(e => e.RotatedAt).HasColumnName("rotated_at");
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.LastSeenAt).HasColumnName("last_seen_at");
        b.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        b.Property(e => e.RevokedAt).HasColumnName("revoked_at");
        b.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(256);

        b.HasOne<Account>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_sessions_accounts");
        b.HasIndex(e => e.AccountId).HasDatabaseName("ix_sessions_account");
        b.HasIndex(e => e.RefreshTokenHash).IsUnique().HasDatabaseName("ux_sessions_refresh");
        b.HasIndex(e => e.PreviousRefreshTokenHash).HasDatabaseName("ix_sessions_previous_refresh");
    }
}

internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> b)
    {
        b.ToTable("consent_records");
        Columns.Keys(b);
        b.Property(e => e.AccountId).HasColumnName("account_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Scope).HasColumnName("scope").HasMaxLength(64).IsRequired();
        b.Property(e => e.PolicyVersion).HasColumnName("policy_version").HasMaxLength(32).IsRequired();
        b.Property(e => e.Law).HasColumnName("law").HasMaxLength(16).IsRequired();
        b.Property(e => e.Granted).HasColumnName("granted");
        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(32).IsRequired();
        b.Property(e => e.RecordedAt).HasColumnName("recorded_at");

        b.HasOne<Account>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_consents_accounts");
        b.HasIndex(e => new { e.AccountId, e.RecordedAt }).HasDatabaseName("ix_consents_account");
    }
}

internal sealed class PendingRegistrationConfiguration : IEntityTypeConfiguration<PendingRegistration>
{
    public void Configure(EntityTypeBuilder<PendingRegistration> b)
    {
        b.ToTable("pending_registrations");
        Columns.Keys(b);
        b.Property(e => e.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        b.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        b.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        b.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        b.Property(e => e.ConsentPolicyVersion).HasColumnName("consent_policy_version").HasMaxLength(32).IsRequired();
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(e => new { e.TenantId, e.Email }).IsUnique().HasDatabaseName("ux_pending_tenant_email");
    }
}

internal sealed class ClaimInviteConfiguration : IEntityTypeConfiguration<ClaimInvite>
{
    public void Configure(EntityTypeBuilder<ClaimInvite> b)
    {
        b.ToTable("claim_invites");
        Columns.Keys(b);
        b.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(Columns.HashLength).IsRequired();
        b.Property(e => e.Institute).HasColumnName("institute").HasMaxLength(200).IsRequired();
        b.Property(e => e.LearnerName).HasColumnName("learner_name").HasMaxLength(200).IsRequired();
        b.Property(e => e.Programme).HasColumnName("programme").HasMaxLength(300).IsRequired();
        b.Property(e => e.Batch).HasColumnName("batch").HasMaxLength(300).IsRequired();
        b.Property(e => e.FromCv).HasColumnName("from_cv").HasMaxLength(300).IsRequired();
        b.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20).IsRequired();
        b.Property(e => e.Certificate).HasColumnName("certificate").HasMaxLength(200).IsRequired();
        b.Property(e => e.DeleteBy).HasColumnName("delete_by");
        b.Property(e => e.Status).HasColumnName("status");
        b.Property(e => e.Stage).HasColumnName("stage");
        b.Property(e => e.AccountId).HasColumnName("account_id").HasColumnType(Columns.Guid);
        b.Property(e => e.SectionsJson).HasColumnName("sections_json").HasColumnType("longtext").IsRequired();
        b.Property(e => e.ResultJson).HasColumnName("result_json").HasColumnType("longtext");
        b.Property(e => e.ConfirmedSlugs).HasColumnName("confirmed_slugs").HasMaxLength(1000).IsRequired();
        b.Property(e => e.ShareProgress).HasColumnName("share_progress");
        b.Property(e => e.VerifyCertificate).HasColumnName("verify_certificate");
        b.Property(e => e.ApplyForMe).HasColumnName("apply_for_me");
        b.Property(e => e.DeclineReason).HasColumnName("decline_reason").HasMaxLength(32);
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("ux_claims_token");
        b.HasIndex(e => e.AccountId).HasDatabaseName("ix_claims_account");
    }
}
