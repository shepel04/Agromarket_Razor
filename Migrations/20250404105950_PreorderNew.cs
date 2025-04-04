using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agromarket.Migrations
{
    /// <inheritdoc />
    public partial class PreorderNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPreorder",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreorderDate",
                table: "OrderItems",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPreorder",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PreorderDate",
                table: "OrderItems");
        }
    }
}
