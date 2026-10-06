using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevStation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificationSeverityMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "metadata",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "severity",
                table: "notifications",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "metadata",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "notifications");
        }
    }
}
