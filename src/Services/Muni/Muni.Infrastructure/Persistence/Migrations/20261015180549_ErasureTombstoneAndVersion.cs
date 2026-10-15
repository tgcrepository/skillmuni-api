using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ErasureTombstoneAndVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "erased_at",
                table: "learners",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "learners",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "erased_at",
                table: "learners");

            migrationBuilder.DropColumn(
                name: "version",
                table: "learners");
        }
    }
}
