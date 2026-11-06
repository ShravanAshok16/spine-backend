using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserBookTags_Tags_TagsId",
                table: "UserBookTags");

            migrationBuilder.DropForeignKey(
                name: "FK_UserBookTags_UserBooks_UserBooksId",
                table: "UserBookTags");

            migrationBuilder.RenameColumn(
                name: "UserBooksId",
                table: "UserBookTags",
                newName: "TagId");

            migrationBuilder.RenameColumn(
                name: "TagsId",
                table: "UserBookTags",
                newName: "UserBookId");

            migrationBuilder.RenameIndex(
                name: "IX_UserBookTags_UserBooksId",
                table: "UserBookTags",
                newName: "IX_UserBookTags_TagId");

            migrationBuilder.AddColumn<bool>(
                name: "IsFavourite",
                table: "UserBooks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LentDate",
                table: "UserBooks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Ownership",
                table: "UserBooks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReadingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PagesRead = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingSessions_UserBooks_UserBookId",
                        column: x => x.UserBookId,
                        principalTable: "UserBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingSessions_UserBookId",
                table: "ReadingSessions",
                column: "UserBookId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserBookTags_Tags_TagId",
                table: "UserBookTags",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserBookTags_UserBooks_UserBookId",
                table: "UserBookTags",
                column: "UserBookId",
                principalTable: "UserBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserBookTags_Tags_TagId",
                table: "UserBookTags");

            migrationBuilder.DropForeignKey(
                name: "FK_UserBookTags_UserBooks_UserBookId",
                table: "UserBookTags");

            migrationBuilder.DropTable(
                name: "ReadingSessions");

            migrationBuilder.DropColumn(
                name: "IsFavourite",
                table: "UserBooks");

            migrationBuilder.DropColumn(
                name: "LentDate",
                table: "UserBooks");

            migrationBuilder.DropColumn(
                name: "Ownership",
                table: "UserBooks");

            migrationBuilder.RenameColumn(
                name: "TagId",
                table: "UserBookTags",
                newName: "UserBooksId");

            migrationBuilder.RenameColumn(
                name: "UserBookId",
                table: "UserBookTags",
                newName: "TagsId");

            migrationBuilder.RenameIndex(
                name: "IX_UserBookTags_TagId",
                table: "UserBookTags",
                newName: "IX_UserBookTags_UserBooksId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserBookTags_Tags_TagsId",
                table: "UserBookTags",
                column: "TagsId",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserBookTags_UserBooks_UserBooksId",
                table: "UserBookTags",
                column: "UserBooksId",
                principalTable: "UserBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
