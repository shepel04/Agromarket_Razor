using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agromarket.Migrations
{
    /// <inheritdoc />
    public partial class Suppliers4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TelegramBotToken",
                table: "Suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramChannelId",
                table: "Suppliers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelegramBotToken",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TelegramChannelId",
                table: "Suppliers");
        }
    }
}
