using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sparkling.Backend.Migrations
{
    /// <inheritdoc />
    public partial class BalanceByHour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BalanceByHour",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BalanceByHour",
                table: "AspNetUsers");
        }
    }
}
