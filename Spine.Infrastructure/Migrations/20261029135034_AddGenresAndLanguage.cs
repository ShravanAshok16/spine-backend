using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGenresAndLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Genres",
                table: "Books",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Books",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Genres",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Books");
        }
    }
}
