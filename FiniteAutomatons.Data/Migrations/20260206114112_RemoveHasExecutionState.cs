using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiniteAutomatons.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHasExecutionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasExecutionState",
                table: "SavedAutomatons");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasExecutionState",
                table: "SavedAutomatons",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
