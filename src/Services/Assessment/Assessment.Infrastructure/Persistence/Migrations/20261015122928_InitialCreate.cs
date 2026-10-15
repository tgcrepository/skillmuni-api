using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assessment.Infrastructure.Persistence.Migrations
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
                name: "attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    number = table.Column<int>(type: "int", nullable: false),
                    taken_on = table.Column<DateTime>(type: "date", nullable: false),
                    scores = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    archetype_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    xp_earned = table.Column<int>(type: "int", nullable: false),
                    answers = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attempts", x => x.id);
                })
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
                name: "journeys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    answers = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    current_pair = table.Column<int>(type: "int", nullable: false),
                    xp = table.Column<int>(type: "int", nullable: false),
                    islands_cleared = table.Column<int>(type: "int", nullable: false),
                    remind = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    remind_changed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journeys", x => x.id);
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

            migrationBuilder.CreateIndex(
                name: "ux_attempts_learner_number",
                table: "attempts",
                columns: new[] { "learner_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journeys_remind",
                table: "journeys",
                columns: new[] { "tenant_id", "remind", "attempts" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_processed_at_occurred_at",
                table: "outbox_messages",
                columns: new[] { "processed_at", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attempts");

            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "journeys");

            migrationBuilder.DropTable(
                name: "outbox_messages");
        }
    }
}
