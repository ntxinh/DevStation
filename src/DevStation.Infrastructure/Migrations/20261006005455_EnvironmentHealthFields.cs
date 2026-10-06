using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevStation.Infrastructure.Migrations;

/// <inheritdoc />
public partial class EnvironmentHealthFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "deployment_provider",
            table: "environments",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "git_branch",
            table: "environments",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "git_commit",
            table: "environments",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "health_check_url",
            table: "environments",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "environments",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "deployment_provider",
            table: "environments");

        migrationBuilder.DropColumn(
            name: "git_branch",
            table: "environments");

        migrationBuilder.DropColumn(
            name: "git_commit",
            table: "environments");

        migrationBuilder.DropColumn(
            name: "health_check_url",
            table: "environments");

        migrationBuilder.DropColumn(
            name: "status",
            table: "environments");
    }
}
