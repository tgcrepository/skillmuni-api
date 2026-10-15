using System;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Initial identity schema. Written by hand in a workspace without the .NET SDK; CI's
    /// <c>dotnet ef migrations has-pending-model-changes</c> step confirms it matches the model.
    /// </summary>
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20261013120000_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    first_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    password_hash = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    google_subject = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    locked_until = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    failed_password_attempts = table.Column<int>(type: "int", nullable: false),
                    market = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false),
                    roles = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_accounts", x => x.id));

            migrationBuilder.CreateTable(
                name: "claim_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    token_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    institute = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    learner_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    programme = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    batch = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    from_cv = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    certificate = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    delete_by = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    stage = table.Column<int>(type: "int", nullable: false),
                    account_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    sections_json = table.Column<string>(type: "longtext", nullable: false),
                    result_json = table.Column<string>(type: "longtext", nullable: true),
                    confirmed_slugs = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    share_progress = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    verify_certificate = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    apply_for_me = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    decline_reason = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_claim_invites", x => x.id));

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    event_type = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "longtext", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    correlation_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    last_error = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_outbox_messages", x => x.id));

            migrationBuilder.CreateTable(
                name: "pending_registrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false),
                    first_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    consent_policy_version = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_pending_registrations", x => x.id));

            migrationBuilder.CreateTable(
                name: "verification_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    purpose = table.Column<int>(type: "int", nullable: false),
                    target = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    code_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    account_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    resend_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tries_left = table.Column<int>(type: "int", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_verification_codes", x => x.id));

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    account_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    scope = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    policy_version = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    law = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    granted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    source = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    recorded_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_consents_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    account_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    previous_refresh_token_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    rotated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    user_agent = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessions_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "ux_accounts_tenant_email", table: "accounts", columns: new[] { "tenant_id", "email" }, unique: true);
            migrationBuilder.CreateIndex(name: "ux_accounts_tenant_google", table: "accounts", columns: new[] { "tenant_id", "google_subject" }, unique: true);
            migrationBuilder.CreateIndex(name: "ux_accounts_tenant_phone", table: "accounts", columns: new[] { "tenant_id", "phone" }, unique: true);
            migrationBuilder.CreateIndex(name: "ix_claims_account", table: "claim_invites", column: "account_id");
            migrationBuilder.CreateIndex(name: "ux_claims_token", table: "claim_invites", column: "token_hash", unique: true);
            migrationBuilder.CreateIndex(name: "ix_consents_account", table: "consent_records", columns: new[] { "account_id", "recorded_at" });
            migrationBuilder.CreateIndex(name: "IX_outbox_messages_processed_at_occurred_at", table: "outbox_messages", columns: new[] { "processed_at", "occurred_at" });
            migrationBuilder.CreateIndex(name: "ux_pending_tenant_email", table: "pending_registrations", columns: new[] { "tenant_id", "email" }, unique: true);
            migrationBuilder.CreateIndex(name: "ix_sessions_account", table: "sessions", column: "account_id");
            migrationBuilder.CreateIndex(name: "ix_sessions_previous_refresh", table: "sessions", column: "previous_refresh_token_hash");
            migrationBuilder.CreateIndex(name: "ux_sessions_refresh", table: "sessions", column: "refresh_token_hash", unique: true);
            migrationBuilder.CreateIndex(name: "ix_codes_hash", table: "verification_codes", columns: new[] { "purpose", "code_hash" });
            migrationBuilder.CreateIndex(name: "ix_codes_target", table: "verification_codes", columns: new[] { "tenant_id", "purpose", "target", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "claim_invites");
            migrationBuilder.DropTable(name: "consent_records");
            migrationBuilder.DropTable(name: "outbox_messages");
            migrationBuilder.DropTable(name: "pending_registrations");
            migrationBuilder.DropTable(name: "sessions");
            migrationBuilder.DropTable(name: "verification_codes");
            migrationBuilder.DropTable(name: "accounts");
        }
    }
}
