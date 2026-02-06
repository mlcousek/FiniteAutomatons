using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiniteAutomatons.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaveModeToSavedAutomaton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SaveMode",
                table: "SavedAutomatons",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaveMode",
                table: "SavedAutomatons");
        }
    }
}
