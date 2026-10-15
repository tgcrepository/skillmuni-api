using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JobProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "job_profile_completed_at",
                table: "learners",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "job_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    about = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true),
                    hide_contact = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    expires_days = table.Column<int>(type: "int", nullable: false),
                    share_code = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    cv_prefill = table.Column<string>(type: "json", nullable: false),
                    cv_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    cv_blob = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    applied_changes = table.Column<string>(type: "json", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_profiles", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ux_job_profiles_learner",
                table: "job_profiles",
                column: "learner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_job_profiles_tenant_share_code",
                table: "job_profiles",
                columns: new[] { "tenant_id", "share_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_profiles");

            migrationBuilder.DropColumn(
                name: "job_profile_completed_at",
                table: "learners");
        }
    }
}
