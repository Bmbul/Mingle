using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mingle.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicAbout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "about",
                table: "topics",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "about",
                table: "topics");
        }
    }
}
