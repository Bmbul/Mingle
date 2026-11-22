using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mingle.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "card_kinds",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    response_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    audience = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_kinds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "decks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name_hy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name_ru = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "topics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pickable = table.Column<bool>(type: "boolean", nullable: false),
                    audience = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name_en = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    name_hy = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    name_ru = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind_id = table.Column<short>(type: "smallint", nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    forked_from_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    machine_en = table.Column<bool>(type: "boolean", nullable: false),
                    machine_hy = table.Column<bool>(type: "boolean", nullable: false),
                    machine_ru = table.Column<bool>(type: "boolean", nullable: false),
                    text_en = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    text_hy = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    text_ru = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cards", x => x.id);
                    table.CheckConstraint("ck_cards_depth", "depth BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "fk_cards_card_kinds_kind_id",
                        column: x => x.kind_id,
                        principalTable: "card_kinds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cards_cards_forked_from_id",
                        column: x => x.forked_from_id,
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "card_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    text_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    text_hy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    text_ru = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_card_options_cards_card_id",
                        column: x => x.card_id,
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "card_topics",
                columns: table => new
                {
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_topics", x => new { x.card_id, x.topic_id });
                    table.ForeignKey(
                        name: "fk_card_topics_cards_card_id",
                        column: x => x.card_id,
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_card_topics_topics_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deck_cards",
                columns: table => new
                {
                    deck_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deck_cards", x => new { x.deck_id, x.card_id });
                    table.ForeignKey(
                        name: "fk_deck_cards_cards_card_id",
                        column: x => x.card_id,
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deck_cards_decks_deck_id",
                        column: x => x.deck_id,
                        principalTable: "decks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "card_kinds",
                columns: new[] { "id", "audience", "code", "response_type" },
                values: new object[,]
                {
                    { (short)1, "FollowTurnStyle", "open", "Free" },
                    { (short)2, "FollowTurnStyle", "finish", "Free" },
                    { (short)3, "Everyone", "wyr", "Choice" },
                    { (short)4, "Everyone", "likely", "PlayerVote" },
                    { (short)5, "AboutTurnPlayer", "guess", "Free" },
                    { (short)6, "FollowTurnStyle", "rate", "Scale" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_card_kinds_code",
                table: "card_kinds",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_card_options_card_id_position",
                table: "card_options",
                columns: new[] { "card_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_card_topics_topic_id",
                table: "card_topics",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ix_cards_forked_from_id",
                table: "cards",
                column: "forked_from_id");

            migrationBuilder.CreateIndex(
                name: "ix_cards_kind_id",
                table: "cards",
                column: "kind_id");

            migrationBuilder.CreateIndex(
                name: "ix_deck_cards_card_id",
                table: "deck_cards",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "ix_topics_code",
                table: "topics",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "card_options");

            migrationBuilder.DropTable(
                name: "card_topics");

            migrationBuilder.DropTable(
                name: "deck_cards");

            migrationBuilder.DropTable(
                name: "topics");

            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "decks");

            migrationBuilder.DropTable(
                name: "card_kinds");
        }
    }
}
