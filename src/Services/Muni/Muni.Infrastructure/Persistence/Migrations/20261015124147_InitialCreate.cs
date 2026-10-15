using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muni.Infrastructure.Persistence.Migrations
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
                name: "learners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    first_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    joined_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    total_xp = table.Column<int>(type: "int", nullable: false),
                    level = table.Column<int>(type: "int", nullable: false),
                    rank_name = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    next_level_xp = table.Column<int>(type: "int", nullable: false),
                    streak_days = table.Column<int>(type: "int", nullable: false),
                    quests_done_mask = table.Column<int>(type: "int", nullable: false),
                    archetype = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learners", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    conversation_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    speaker = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false),
                    text = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    topic = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    reply = table.Column<string>(type: "json", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
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
                name: "ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    message_ref = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    helpful = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    rated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ratings", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_messages_created",
                table: "messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_messages_learner_created",
                table: "messages",
                columns: new[] { "learner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_processed_at_occurred_at",
                table: "outbox_messages",
                columns: new[] { "processed_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ratings_rated",
                table: "ratings",
                column: "rated_at");

            migrationBuilder.CreateIndex(
                name: "ux_ratings_learner_message",
                table: "ratings",
                columns: new[] { "learner_id", "message_ref" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "learners");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "ratings");
        }
    }
}
