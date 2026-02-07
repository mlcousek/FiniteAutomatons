using Microsoft.EntityFrameworkCore.Migrations;

namespace FiniteAutomatons.Data.Migrations;

#nullable disable

public partial class AddNotificationPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "EnableInvitationNotifications",
            table: "AspNetUsers",
            type: "bit",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EnableInvitationNotifications",
            table: "AspNetUsers");
    }
}
