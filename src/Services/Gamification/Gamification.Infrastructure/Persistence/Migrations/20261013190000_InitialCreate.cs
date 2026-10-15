using System;
using Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gamification.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Initial gamification schema. Written without the .NET SDK (generated from the same spec as the entity
    /// configurations); CI's <c>dotnet ef migrations has-pending-model-changes</c> step confirms it matches the model.
    /// </summary>
    [DbContext(typeof(GamificationDbContext))]
    [Migration("20261013190000_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    market = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false),
                    photo_url = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    batch = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    total_xp = table.Column<int>(type: "int", nullable: false),
                    badges = table.Column<int>(type: "int", nullable: false),
                    streak_days = table.Column<int>(type: "int", nullable: false),
                    best_streak = table.Column<int>(type: "int", nullable: false),
                    last_active_day = table.Column<DateTime>(type: "date", nullable: true),
                    learning_minutes = table.Column<int>(type: "int", nullable: false),
                    cheers_received = table.Column<int>(type: "int", nullable: false),
                    last_seen_rank = table.Column<int>(type: "int", nullable: true),
                    showcase = table.Column<string>(type: "json", nullable: false),
                    counters = table.Column<string>(type: "json", nullable: false),
                    joined_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_players", x => x.id));

            migrationBuilder.CreateTable(
                name: "xp_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    reason = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    source_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    xp = table.Column<int>(type: "int", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_xp_entries", x => x.id));

            migrationBuilder.CreateTable(
                name: "badge_awards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    badge_id = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    earned_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_badge_awards", x => x.id));

            migrationBuilder.CreateTable(
                name: "active_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    day = table.Column<DateTime>(type: "date", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_active_days", x => x.id));

            migrationBuilder.CreateTable(
                name: "daily_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    day = table.Column<DateTime>(type: "date", nullable: false),
                    quest_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    value = table.Column<int>(type: "int", nullable: false),
                    completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_daily_progress", x => x.id));

            migrationBuilder.CreateTable(
                name: "cheers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    from_learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    to_learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_cheers", x => x.id));

            migrationBuilder.CreateTable(
                name: "challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    from_learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    to_learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_challenges", x => x.id));

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    event_type = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_inbox_messages", x => x.id));

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

            migrationBuilder.CreateIndex(name: "ix_players_batch", table: "players", columns: new[] { "tenant_id", "batch" });
            migrationBuilder.CreateIndex(name: "ux_xp_entries_reason", table: "xp_entries", columns: new[] { "learner_id", "reason", "source_id" }, unique: true);
            migrationBuilder.CreateIndex(name: "ix_xp_entries_board", table: "xp_entries", columns: new[] { "tenant_id", "occurred_at" });
            migrationBuilder.CreateIndex(name: "ix_xp_entries_learner", table: "xp_entries", columns: new[] { "learner_id", "occurred_at" });
            migrationBuilder.CreateIndex(name: "ux_badge_awards_learner_badge", table: "badge_awards", columns: new[] { "learner_id", "badge_id" }, unique: true);
            migrationBuilder.CreateIndex(name: "ix_badge_awards_badge", table: "badge_awards", columns: new[] { "tenant_id", "badge_id" });
            migrationBuilder.CreateIndex(name: "ux_active_days_learner_day", table: "active_days", columns: new[] { "learner_id", "day" }, unique: true);
            migrationBuilder.CreateIndex(name: "ux_daily_progress_learner_day_quest", table: "daily_progress", columns: new[] { "learner_id", "day", "quest_id" }, unique: true);
            migrationBuilder.CreateIndex(name: "ux_cheers_from_to", table: "cheers", columns: new[] { "from_learner_id", "to_learner_id" }, unique: true);
            migrationBuilder.CreateIndex(name: "ix_cheers_to", table: "cheers", column: "to_learner_id");
            migrationBuilder.CreateIndex(name: "ix_challenges_pair", table: "challenges", columns: new[] { "from_learner_id", "to_learner_id", "ends_at" });
            migrationBuilder.CreateIndex(name: "ix_challenges_to", table: "challenges", column: "to_learner_id");
            migrationBuilder.CreateIndex(name: "IX_outbox_messages_processed_at_occurred_at", table: "outbox_messages", columns: new[] { "processed_at", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "players");
            migrationBuilder.DropTable(name: "xp_entries");
            migrationBuilder.DropTable(name: "badge_awards");
            migrationBuilder.DropTable(name: "active_days");
            migrationBuilder.DropTable(name: "daily_progress");
            migrationBuilder.DropTable(name: "cheers");
            migrationBuilder.DropTable(name: "challenges");
            migrationBuilder.DropTable(name: "inbox_messages");
            migrationBuilder.DropTable(name: "outbox_messages");
        }
    }
}
