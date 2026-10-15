using System;
using Learner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Initial learner schema. Written without the .NET SDK (generated from the same spec as the entity
    /// configurations); CI's <c>dotnet ef migrations has-pending-model-changes</c> step confirms it matches the model.
    /// </summary>
    [DbContext(typeof(LearnerDbContext))]
    [Migration("20261013150000_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    first_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    market = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false),
                    phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true),
                    photo_blob = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    photo_version = table.Column<int>(type: "int", nullable: false),
                    gender = table.Column<int>(type: "int", nullable: true),
                    date_of_birth = table.Column<DateTime>(type: "date", nullable: true),
                    profile_skills = table.Column<string>(type: "json", nullable: false),
                    quests_completed = table.Column<int>(type: "int", nullable: false),
                    parts_done = table.Column<int>(type: "int", nullable: false),
                    programme_wrong_tries = table.Column<int>(type: "int", nullable: false),
                    programme_locked_until = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    sounds_on = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    voice_on = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    language = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    xp = table.Column<int>(type: "int", nullable: false),
                    level = table.Column<int>(type: "int", nullable: false),
                    rank_name = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    level_start_xp = table.Column<int>(type: "int", nullable: false),
                    next_level_xp = table.Column<int>(type: "int", nullable: false),
                    badges = table.Column<int>(type: "int", nullable: false),
                    streak_days = table.Column<int>(type: "int", nullable: false),
                    estimated_xp = table.Column<int>(type: "int", nullable: false),
                    estimated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    archetype_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    archetype_summary = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    joined_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_learners", x => x.id));

            migrationBuilder.CreateTable(
                name: "quest_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    slug = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    answers = table.Column<string>(type: "json", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_quest_answers", x => x.id));

            migrationBuilder.CreateTable(
                name: "drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    draft_key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "json", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_drafts", x => x.id));

            migrationBuilder.CreateTable(
                name: "linked_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    document = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    masked = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    last4 = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false),
                    birthday = table.Column<DateTime>(type: "date", nullable: true),
                    verified_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    verified_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_linked_identities", x => x.id));

            migrationBuilder.CreateTable(
                name: "programmes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    partner = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    centre = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    batch_starts = table.Column<DateTime>(type: "date", nullable: false),
                    duration = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    stipend = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    marks_from = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false),
                    min_english = table.Column<int>(type: "int", nullable: false),
                    min_maths = table.Column<int>(type: "int", nullable: false),
                    eligibility = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_programmes", x => x.id));

            migrationBuilder.CreateTable(
                name: "enrolments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    programme_code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    marks_from = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false),
                    english = table.Column<int>(type: "int", nullable: false),
                    maths = table.Column<int>(type: "int", nullable: false),
                    proficiency = table.Column<string>(type: "json", nullable: false),
                    bank_holder = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    bank_account_type = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    bank_account_encrypted = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    bank_account_last4 = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true),
                    bank_routing = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    upi = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_enrolments", x => x.id));

            migrationBuilder.CreateTable(
                name: "feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    mood = table.Column<int>(type: "int", nullable: false),
                    tags = table.Column<string>(type: "json", nullable: false),
                    comment = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    screen = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    xp_awarded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_feedback", x => x.id));

            migrationBuilder.CreateTable(
                name: "trophy_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    blob_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    file_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_trophy_files", x => x.id));

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

            migrationBuilder.CreateIndex(name: "ix_learners_tenant", table: "learners", column: "tenant_id");
            migrationBuilder.CreateIndex(name: "ux_quest_answers_learner_slug", table: "quest_answers", columns: new[] { "learner_id", "slug" }, unique: true);
            migrationBuilder.CreateIndex(name: "ux_drafts_learner_key", table: "drafts", columns: new[] { "learner_id", "draft_key" }, unique: true);
            migrationBuilder.CreateIndex(name: "ux_linked_identities_learner", table: "linked_identities", column: "learner_id", unique: true);
            migrationBuilder.CreateIndex(name: "ux_programmes_tenant_code", table: "programmes", columns: new[] { "tenant_id", "code" }, unique: true);
            migrationBuilder.CreateIndex(name: "ux_enrolments_learner_programme", table: "enrolments", columns: new[] { "learner_id", "programme_code" }, unique: true);
            migrationBuilder.CreateIndex(name: "ix_feedback_learner_xp", table: "feedback", columns: new[] { "learner_id", "xp_awarded", "created_at" });
            migrationBuilder.CreateIndex(name: "ix_trophy_files_learner", table: "trophy_files", column: "learner_id");
            migrationBuilder.CreateIndex(name: "IX_outbox_messages_processed_at_occurred_at", table: "outbox_messages", columns: new[] { "processed_at", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "learners");
            migrationBuilder.DropTable(name: "quest_answers");
            migrationBuilder.DropTable(name: "drafts");
            migrationBuilder.DropTable(name: "linked_identities");
            migrationBuilder.DropTable(name: "programmes");
            migrationBuilder.DropTable(name: "enrolments");
            migrationBuilder.DropTable(name: "feedback");
            migrationBuilder.DropTable(name: "trophy_files");
            migrationBuilder.DropTable(name: "inbox_messages");
            migrationBuilder.DropTable(name: "outbox_messages");
        }
    }
}
