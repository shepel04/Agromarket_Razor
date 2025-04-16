using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agromarket.Migrations
{
    /// <inheritdoc />
    public partial class RepDisc1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarehouseEntryId",
                table: "OrderItems",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarehouseEntryId",
                table: "OrderItems");
        }
    }
}
