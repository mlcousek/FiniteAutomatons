using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiniteAutomatons.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceRegexToAutomatons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceRegex",
                table: "SharedAutomatons",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRegex",
                table: "SavedAutomatons",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableInvitationNotifications",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceRegex",
                table: "SharedAutomatons");

            migrationBuilder.DropColumn(
                name: "SourceRegex",
                table: "SavedAutomatons");

            migrationBuilder.DropColumn(
                name: "EnableInvitationNotifications",
                table: "AspNetUsers");
        }
    }
}
