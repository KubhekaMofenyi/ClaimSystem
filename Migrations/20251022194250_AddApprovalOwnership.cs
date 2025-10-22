using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoordinatorUserId",
                table: "Claims",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LecturerUserId",
                table: "Claims",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManagerUserId",
                table: "Claims",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoordinatorUserId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "LecturerUserId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ManagerUserId",
                table: "Claims");
        }
    }
}
