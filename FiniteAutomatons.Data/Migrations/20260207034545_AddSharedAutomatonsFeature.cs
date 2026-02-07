using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiniteAutomatons.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedAutomatonsFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedAutomatonGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    InviteCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultRoleForInvite = table.Column<int>(type: "int", nullable: false),
                    IsInviteLinkActive = table.Column<bool>(type: "bit", nullable: false),
                    InviteLinkExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedAutomatonGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedAutomatons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SaveMode = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ExecutionStateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedAutomatons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedAutomatonGroupInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    InvitedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Token = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedAutomatonGroupInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedAutomatonGroupInvitations_SharedAutomatonGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SharedAutomatonGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedAutomatonGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    InvitedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedAutomatonGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedAutomatonGroupMembers_SharedAutomatonGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SharedAutomatonGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedAutomatonGroupAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AutomatonId = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedAutomatonGroupAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedAutomatonGroupAssignments_SharedAutomatonGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SharedAutomatonGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedAutomatonGroupAssignments_SharedAutomatons_AutomatonId",
                        column: x => x.AutomatonId,
                        principalTable: "SharedAutomatons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharedAutomatonGroupAssignments_AutomatonId_GroupId",
                table: "SharedAutomatonGroupAssignments",
                columns: new[] { "AutomatonId", "GroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedAutomatonGroupAssignments_GroupId",
                table: "SharedAutomatonGroupAssignments",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedAutomatonGroupInvitations_GroupId_Email_Status",
                table: "SharedAutomatonGroupInvitations",
                columns: new[] { "GroupId", "Email", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedAutomatonGroupInvitations_Token",
                table: "SharedAutomatonGroupInvitations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedAutomatonGroupMembers_GroupId_UserId",
                table: "SharedAutomatonGroupMembers",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedAutomatonGroups_InviteCode",
                table: "SharedAutomatonGroups",
                column: "InviteCode",
                unique: true,
                filter: "[InviteCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharedAutomatonGroupAssignments");

            migrationBuilder.DropTable(
                name: "SharedAutomatonGroupInvitations");

            migrationBuilder.DropTable(
                name: "SharedAutomatonGroupMembers");

            migrationBuilder.DropTable(
                name: "SharedAutomatons");

            migrationBuilder.DropTable(
                name: "SharedAutomatonGroups");
        }
    }
}
