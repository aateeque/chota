using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chota.Api.Migrations
{
    /// <inheritdoc />
    public partial class UrlHashField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LongUrlHash",
                table: "ShortUrls",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LongUrlHash",
                table: "ShortUrls");
        }
    }
}
