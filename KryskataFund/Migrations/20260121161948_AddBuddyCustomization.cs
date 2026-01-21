using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KryskataFund.Migrations
{
    /// <inheritdoc />
    public partial class AddBuddyCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BuddyGlasses",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuddyHat",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuddyMask",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuddyGlasses",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BuddyHat",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BuddyMask",
                table: "Users");
        }
    }
}
