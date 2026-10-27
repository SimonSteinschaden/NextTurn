using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextTurn.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OwnershipAndGameNights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Action",
                table: "Interactions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "IsOwned",
                table: "Interactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GameNights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Players = table.Column<int>(type: "integer", nullable: false),
                    IncludeUnowned = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameNights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameNights_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameNightChoices",
                columns: table => new
                {
                    GameNightId = table.Column<Guid>(type: "uuid", nullable: false),
                    BggId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameNightChoices", x => new { x.GameNightId, x.BggId });
                    table.ForeignKey(
                        name: "FK_GameNightChoices_GameNights_GameNightId",
                        column: x => x.GameNightId,
                        principalTable: "GameNights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameNightChoices_Games_BggId",
                        column: x => x.BggId,
                        principalTable: "Games",
                        principalColumn: "BggId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_SessionId_IsOwned",
                table: "Interactions",
                columns: new[] { "SessionId", "IsOwned" });

            migrationBuilder.CreateIndex(
                name: "IX_GameNightChoices_BggId",
                table: "GameNightChoices",
                column: "BggId");

            migrationBuilder.CreateIndex(
                name: "IX_GameNightChoices_GameNightId_Action",
                table: "GameNightChoices",
                columns: new[] { "GameNightId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_GameNights_SessionId_CreatedAt",
                table: "GameNights",
                columns: new[] { "SessionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameNightChoices");

            migrationBuilder.DropTable(
                name: "GameNights");

            migrationBuilder.DropIndex(
                name: "IX_Interactions_SessionId_IsOwned",
                table: "Interactions");

            migrationBuilder.DropColumn(
                name: "IsOwned",
                table: "Interactions");

            migrationBuilder.AlterColumn<int>(
                name: "Action",
                table: "Interactions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
