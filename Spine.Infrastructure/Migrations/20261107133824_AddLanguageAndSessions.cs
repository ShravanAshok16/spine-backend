using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mood",
                table: "ReadingSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "Books",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalTitle",
                table: "Books",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransliteratedTitle",
                table: "Books",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mood",
                table: "ReadingSessions");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "OriginalTitle",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "TransliteratedTitle",
                table: "Books");
        }
    }
}
