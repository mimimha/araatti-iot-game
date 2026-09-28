using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AraAtti.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    email = table.Column<string>(type: "varchar(190)", maxLength: 190, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    name = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    skin_color = table.Column<string>(type: "char(7)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    slot_index = table.Column<byte>(type: "tinyint unsigned", nullable: false, defaultValue: (byte)0),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.id);
                    table.ForeignKey(
                        name: "fk_characters_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "character_parts",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    character_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    slot = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    prefab_name = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_parts", x => x.id);
                    table.ForeignKey(
                        name: "fk_parts_character",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "uk_parts_character_slot",
                table: "character_parts",
                columns: new[] { "character_id", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_characters_user_id",
                table: "characters",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uk_characters_name",
                table: "characters",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uk_characters_user_slot",
                table: "characters",
                columns: new[] { "user_id", "slot_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uk_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_parts");

            migrationBuilder.DropTable(
                name: "characters");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
