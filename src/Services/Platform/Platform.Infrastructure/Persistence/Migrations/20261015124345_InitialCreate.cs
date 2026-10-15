using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    event_type = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    channel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    template = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    recipient = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    provider = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    provider_message_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    queue_message_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    push = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    whatsapp = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    sms = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    email = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    kind = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    body = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    link = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    source_key = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    read_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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
                    last_error = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tenant_themes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    logo_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    brand = table.Column<string>(type: "json", nullable: false),
                    accent = table.Column<string>(type: "json", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_themes", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "tenant_themes",
                columns: new[] { "id", "accent", "brand", "logo_url", "name", "tenant_id", "updated_at" },
                values: new object[,]
                {
                    { new Guid("5b1f0c2e-7d3a-4e8b-9c61-0a2f4d8e1b01"), "{}", "{}", null, "Skillmuni", "skillmuni-in", new DateTime(2026, 10, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5b1f0c2e-7d3a-4e8b-9c61-0a2f4d8e1b02"), "{}", "{}", null, "Skillmuni", "skillmuni-za", new DateTime(2026, 10, 15, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_created",
                table: "notification_deliveries",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_notification_deliveries_queue_message",
                table: "notification_deliveries",
                column: "queue_message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_learner_created",
                table: "notifications",
                columns: new[] { "learner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_learner_read",
                table: "notifications",
                columns: new[] { "learner_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ux_notifications_learner_source",
                table: "notifications",
                columns: new[] { "learner_id", "source_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_processed_at_occurred_at",
                table: "outbox_messages",
                columns: new[] { "processed_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_themes_tenant",
                table: "tenant_themes",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "tenant_themes");
        }
    }
}
