using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiniteAutomatons.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedAutomatonGroupAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavedAutomatons_SavedAutomatonGroups_GroupId",
                table: "SavedAutomatons");

            migrationBuilder.AddColumn<bool>(
                name: "MembersCanShare",
                table: "SavedAutomatonGroups",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "SavedAutomatonGroupAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AutomatonId = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedAutomatonGroupAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedAutomatonGroupAssignments_SavedAutomatonGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SavedAutomatonGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedAutomatonGroupAssignments_SavedAutomatons_AutomatonId",
                        column: x => x.AutomatonId,
                        principalTable: "SavedAutomatons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedAutomatonGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsModerator = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedAutomatonGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedAutomatonGroupMembers_SavedAutomatonGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SavedAutomatonGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedAutomatonGroupAssignments_AutomatonId",
                table: "SavedAutomatonGroupAssignments",
                column: "AutomatonId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedAutomatonGroupAssignments_GroupId",
                table: "SavedAutomatonGroupAssignments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedAutomatonGroupMembers_GroupId",
                table: "SavedAutomatonGroupMembers",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_SavedAutomatons_SavedAutomatonGroups_GroupId",
                table: "SavedAutomatons",
                column: "GroupId",
                principalTable: "SavedAutomatonGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavedAutomatons_SavedAutomatonGroups_GroupId",
                table: "SavedAutomatons");

            migrationBuilder.DropTable(
                name: "SavedAutomatonGroupAssignments");

            migrationBuilder.DropTable(
                name: "SavedAutomatonGroupMembers");

            migrationBuilder.DropColumn(
                name: "MembersCanShare",
                table: "SavedAutomatonGroups");

            migrationBuilder.AddForeignKey(
                name: "FK_SavedAutomatons_SavedAutomatonGroups_GroupId",
                table: "SavedAutomatons",
                column: "GroupId",
                principalTable: "SavedAutomatonGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
