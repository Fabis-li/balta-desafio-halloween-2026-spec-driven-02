using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkShortener.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveManagementKeyHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagementKeyHash",
                table: "Links");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagementKeyHash",
                table: "Links",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
