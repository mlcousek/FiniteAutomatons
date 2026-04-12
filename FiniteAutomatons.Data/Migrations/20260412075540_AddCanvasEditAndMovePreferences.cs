using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiniteAutomatons.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCanvasEditAndMovePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanvasEditModeEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanvasMoveEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanvasWheelZoomEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanvasEditModeEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CanvasMoveEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CanvasWheelZoomEnabled",
                table: "AspNetUsers");
        }
    }
}
