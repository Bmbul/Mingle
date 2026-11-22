using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mingle.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDeckEditKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "edit_key_hash",
                table: "decks",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "decks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "edit_key_hash",
                table: "decks");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "decks");
        }
    }
}
