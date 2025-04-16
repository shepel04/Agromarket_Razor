using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agromarket.Migrations
{
    /// <inheritdoc />
    public partial class Suppliers5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplyProduct_Products_ProductId",
                table: "SupplyProduct");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplyProduct_Suppliers_SupplierId",
                table: "SupplyProduct");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SupplyProduct",
                table: "SupplyProduct");

            migrationBuilder.RenameTable(
                name: "SupplyProduct",
                newName: "SupplyProducts");

            migrationBuilder.RenameIndex(
                name: "IX_SupplyProduct_SupplierId",
                table: "SupplyProducts",
                newName: "IX_SupplyProducts_SupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_SupplyProduct_ProductId",
                table: "SupplyProducts",
                newName: "IX_SupplyProducts_ProductId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SupplyProducts",
                table: "SupplyProducts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplyProducts_Products_ProductId",
                table: "SupplyProducts",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplyProducts_Suppliers_SupplierId",
                table: "SupplyProducts",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplyProducts_Products_ProductId",
                table: "SupplyProducts");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplyProducts_Suppliers_SupplierId",
                table: "SupplyProducts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SupplyProducts",
                table: "SupplyProducts");

            migrationBuilder.RenameTable(
                name: "SupplyProducts",
                newName: "SupplyProduct");

            migrationBuilder.RenameIndex(
                name: "IX_SupplyProducts_SupplierId",
                table: "SupplyProduct",
                newName: "IX_SupplyProduct_SupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_SupplyProducts_ProductId",
                table: "SupplyProduct",
                newName: "IX_SupplyProduct_ProductId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SupplyProduct",
                table: "SupplyProduct",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplyProduct_Products_ProductId",
                table: "SupplyProduct",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplyProduct_Suppliers_SupplierId",
                table: "SupplyProduct",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
