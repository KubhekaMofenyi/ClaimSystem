using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaimStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClaimId = table.Column<int>(type: "INTEGER", nullable: false),
                    From = table.Column<int>(type: "INTEGER", nullable: false),
                    To = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimStatusHistories_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimStatusHistories_ClaimId",
                table: "ClaimStatusHistories",
                column: "ClaimId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimStatusHistories");
        }
    }
}
