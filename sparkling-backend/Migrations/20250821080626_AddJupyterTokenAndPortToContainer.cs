using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sparkling.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddJupyterTokenAndPortToContainer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JupyterPort",
                table: "Containers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JupyterToken",
                table: "Containers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JupyterPort",
                table: "Containers");

            migrationBuilder.DropColumn(
                name: "JupyterToken",
                table: "Containers");
        }
    }
}
