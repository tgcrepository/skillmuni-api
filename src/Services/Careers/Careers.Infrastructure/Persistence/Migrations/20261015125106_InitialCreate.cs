using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Careers.Infrastructure.Persistence.Migrations
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
                name: "cv_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    blob_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cv_files", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fest_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    fest_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    remind_whatsapp = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    remind_sms = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    remind_calendar = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    cv_label = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fest_bookings", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fest_outcomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    fest_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    employer_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    outcome = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fest_outcomes", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fest_ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    fest_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    stars = table.Column<int>(type: "int", nullable: false),
                    rated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fest_ratings", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fest_waitlist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    fest_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    employer_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    auto_book = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    whatsapp = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    joined_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fest_waitlist", x => x.id);
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
                name: "invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    job_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    recruiter_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    recruiter_initials = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false),
                    recruiter_title = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    interview_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    message = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    respond_by = table.Column<DateTime>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    responded_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "job_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    job_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                    cv_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    cv_name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    answers = table.Column<string>(type: "json", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    invitation_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    fest_offer_key = table.Column<string>(type: "varchar(90)", maxLength: 90, nullable: true),
                    applied_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    withdrawn_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_applications", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "learners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    assessment_done = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    assessment_attempt = table.Column<int>(type: "int", nullable: false),
                    archetype_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                    archetype_summary = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    top_drivers = table.Column<string>(type: "json", nullable: false),
                    assessment_taken_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    job_profile_completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    claim_confirmed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    jobs_seen_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    job_alerts_on = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    job_alerts_frequency = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    job_alert_searches = table.Column<string>(type: "json", nullable: false),
                    fest_alerts_city = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    fest_alerts_online = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    published_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    discoverable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    invites_allowed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learners", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "match_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    match_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_states", x => x.id);
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
                name: "saved_fests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    fest_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    saved_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_fests", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "saved_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    job_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    saved_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_jobs", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fest_picks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    booking_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    fest_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    employer_id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    date = table.Column<DateTime>(type: "date", nullable: false),
                    time = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fest_picks", x => x.id);
                    table.ForeignKey(
                        name: "FK_fest_picks_fest_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "fest_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_cv_files_learner",
                table: "cv_files",
                columns: new[] { "learner_id", "uploaded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_fest_bookings_fest",
                table: "fest_bookings",
                columns: new[] { "tenant_id", "fest_id" });

            migrationBuilder.CreateIndex(
                name: "ux_fest_bookings_learner_fest",
                table: "fest_bookings",
                columns: new[] { "learner_id", "fest_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fest_outcomes_learner_interview",
                table: "fest_outcomes",
                columns: new[] { "learner_id", "fest_id", "employer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fest_picks_learner",
                table: "fest_picks",
                column: "learner_id");

            migrationBuilder.CreateIndex(
                name: "ix_fest_picks_slot",
                table: "fest_picks",
                columns: new[] { "tenant_id", "fest_id", "employer_id", "date", "time" });

            migrationBuilder.CreateIndex(
                name: "ux_fest_picks_booking_employer",
                table: "fest_picks",
                columns: new[] { "booking_id", "employer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fest_ratings_learner_fest",
                table: "fest_ratings",
                columns: new[] { "learner_id", "fest_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fest_waitlist_queue",
                table: "fest_waitlist",
                columns: new[] { "tenant_id", "fest_id", "employer_id", "joined_at" });

            migrationBuilder.CreateIndex(
                name: "ux_fest_waitlist_learner_employer",
                table: "fest_waitlist",
                columns: new[] { "learner_id", "fest_id", "employer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invitations_learner",
                table: "invitations",
                columns: new[] { "learner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_learner_job",
                table: "job_applications",
                columns: new[] { "learner_id", "job_id", "applied_at" });

            migrationBuilder.CreateIndex(
                name: "ux_job_applications_fest_offer",
                table: "job_applications",
                columns: new[] { "learner_id", "fest_offer_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_match_states_learner_match",
                table: "match_states",
                columns: new[] { "learner_id", "match_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_processed_at_occurred_at",
                table: "outbox_messages",
                columns: new[] { "processed_at", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_saved_fests_learner_fest",
                table: "saved_fests",
                columns: new[] { "learner_id", "fest_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_saved_jobs_learner_job",
                table: "saved_jobs",
                columns: new[] { "learner_id", "job_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cv_files");

            migrationBuilder.DropTable(
                name: "fest_outcomes");

            migrationBuilder.DropTable(
                name: "fest_picks");

            migrationBuilder.DropTable(
                name: "fest_ratings");

            migrationBuilder.DropTable(
                name: "fest_waitlist");

            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "job_applications");

            migrationBuilder.DropTable(
                name: "learners");

            migrationBuilder.DropTable(
                name: "match_states");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "saved_fests");

            migrationBuilder.DropTable(
                name: "saved_jobs");

            migrationBuilder.DropTable(
                name: "fest_bookings");
        }
    }
}
