using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sparkling.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddJupyterTokenAndPortToWorkSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JupyterPort",
                table: "WorkSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JupyterToken",
                table: "WorkSessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JupyterPort",
                table: "WorkSessions");

            migrationBuilder.DropColumn(
                name: "JupyterToken",
                table: "WorkSessions");
        }
    }
}
