using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mingle.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddBlitzChallengeKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "card_kinds",
                columns: new[] { "id", "audience", "code", "response_type" },
                values: new object[,]
                {
                    { (short)10, "FollowTurnStyle", "name3", "Free" },
                    { (short)11, "FollowTurnStyle", "letter", "Free" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "card_kinds",
                keyColumn: "id",
                keyValue: (short)10);

            migrationBuilder.DeleteData(
                table: "card_kinds",
                keyColumn: "id",
                keyValue: (short)11);
        }
    }
}
