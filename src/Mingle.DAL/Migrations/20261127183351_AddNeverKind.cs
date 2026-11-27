using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mingle.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddNeverKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "card_kinds",
                columns: new[] { "id", "audience", "code", "response_type" },
                values: new object[] { (short)16, "Everyone", "never", "Free" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "card_kinds",
                keyColumn: "id",
                keyValue: (short)16);
        }
    }
}
