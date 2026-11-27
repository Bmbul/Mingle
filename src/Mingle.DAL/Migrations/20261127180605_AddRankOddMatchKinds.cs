using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mingle.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddRankOddMatchKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "card_kinds",
                columns: new[] { "id", "audience", "code", "response_type" },
                values: new object[,]
                {
                    { (short)13, "AboutTurnPlayer", "rank", "Free" },
                    { (short)14, "Everyone", "odd", "Free" },
                    { (short)15, "Everyone", "match", "Free" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "card_kinds",
                keyColumn: "id",
                keyValue: (short)13);

            migrationBuilder.DeleteData(
                table: "card_kinds",
                keyColumn: "id",
                keyValue: (short)14);

            migrationBuilder.DeleteData(
                table: "card_kinds",
                keyColumn: "id",
                keyValue: (short)15);
        }
    }
}
