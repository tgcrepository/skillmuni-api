using System;
using Cv.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cv.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Initial cv schema. Written without the .NET SDK (generated from the same spec as the entity
    /// configurations); CI's <c>dotnet ef migrations has-pending-model-changes</c> step confirms it matches the model.
    /// </summary>
    [DbContext(typeof(CvDbContext))]
    [Migration("20261013170000_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cv_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    tenant_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    learner_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    market = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false),
                    file_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    blob_names = table.Column<string>(type: "json", nullable: false),
                    state = table.Column<int>(type: "int", nullable: false),
                    step = table.Column<int>(type: "int", nullable: false),
                    quip = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    outcome = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    detail = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    extract = table.Column<string>(type: "json", nullable: true),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    claimed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                },
                constraints: table => table.PrimaryKey("PK_cv_documents", x => x.id));

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

            migrationBuilder.CreateIndex(name: "ix_cv_documents_learner", table: "cv_documents", columns: new[] { "learner_id", "created_at" });
            migrationBuilder.CreateIndex(name: "ix_cv_documents_queue", table: "cv_documents", columns: new[] { "state", "created_at" });
            migrationBuilder.CreateIndex(name: "IX_outbox_messages_processed_at_occurred_at", table: "outbox_messages", columns: new[] { "processed_at", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "cv_documents");
            migrationBuilder.DropTable(name: "inbox_messages");
            migrationBuilder.DropTable(name: "outbox_messages");
        }
    }
}
