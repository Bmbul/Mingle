using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mingle.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMirrorAuctionKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "card_kinds",
                columns: new[] { "id", "audience", "code", "response_type" },
                values: new object[,]
                {
                    { (short)17, "AboutTurnPlayer", "mirror", "Scale" },
                    { (short)18, "Everyone", "lot", "Free" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "card_kinds",
                keyColumn: "id",
                keyValue: (short)17);

            migrationBuilder.DeleteData(
                table: "card_kinds",
                keyColumn: "id",
                keyValue: (short)18);
        }
    }
}
