using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Persistence.Migrations
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
                name: "activity_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    day = table.Column<DateTime>(type: "date", nullable: false),
                    minutes = table.Column<int>(type: "int", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_days", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "brief_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    brief_id = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    module_slug = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    topic_slug = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    brief_index = table.Column<int>(type: "int", nullable: false),
                    bookmarked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    helpful = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    passed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    xp = table.Column<int>(type: "int", nullable: false),
                    last_correct = table.Column<int>(type: "int", nullable: true),
                    last_total = table.Column<int>(type: "int", nullable: true),
                    last_submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brief_progress", x => x.id);
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
                name: "lobby_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    assessment_done_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    jobs_visited_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    first_moves_claimed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lobby_states", x => x.id);
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
                name: "quiz_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    quiz_id = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    correct = table.Column<int>(type: "int", nullable: false),
                    total = table.Column<int>(type: "int", nullable: false),
                    xp = table.Column<int>(type: "int", nullable: false),
                    first_attempt = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_attempts", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "quiz_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    quiz_id = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    answers = table.Column<string>(type: "json", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_drafts", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ux_activity_days_learner_day",
                table: "activity_days",
                columns: new[] { "learner_id", "day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_brief_progress_learner_module",
                table: "brief_progress",
                columns: new[] { "learner_id", "module_slug" });

            migrationBuilder.CreateIndex(
                name: "ix_brief_progress_learner_passed",
                table: "brief_progress",
                columns: new[] { "learner_id", "passed_at" });

            migrationBuilder.CreateIndex(
                name: "ux_brief_progress_learner_brief",
                table: "brief_progress",
                columns: new[] { "learner_id", "brief_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_lobby_states_learner",
                table: "lobby_states",
                column: "learner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_processed_at_occurred_at",
                table: "outbox_messages",
                columns: new[] { "processed_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_quiz_attempts_learner_quiz",
                table: "quiz_attempts",
                columns: new[] { "learner_id", "quiz_id" });

            migrationBuilder.CreateIndex(
                name: "ux_quiz_drafts_learner_quiz",
                table: "quiz_drafts",
                columns: new[] { "learner_id", "quiz_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_days");

            migrationBuilder.DropTable(
                name: "brief_progress");

            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "lobby_states");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "quiz_attempts");

            migrationBuilder.DropTable(
                name: "quiz_drafts");
        }
    }
}
