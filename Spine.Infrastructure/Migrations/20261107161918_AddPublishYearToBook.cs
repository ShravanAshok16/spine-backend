using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishYearToBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PublishYear",
                table: "Books",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishYear",
                table: "Books");
        }
    }
}
