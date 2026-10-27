using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextTurn.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameDesigners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Designers",
                table: "Games",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]
            );

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DesignersUpdatedAt",
                table: "Games",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder
                .CreateIndex(name: "IX_Games_Designers", table: "Games", column: "Designers")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Games_Designers", table: "Games");

            migrationBuilder.DropColumn(name: "Designers", table: "Games");

            migrationBuilder.DropColumn(name: "DesignersUpdatedAt", table: "Games");
        }
    }
}
