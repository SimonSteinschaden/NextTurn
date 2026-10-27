using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NextTurn.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase().Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    BggId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    YearPublished = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    BggRank = table.Column<int>(type: "integer", nullable: true),
                    AverageRating = table.Column<double>(type: "double precision", nullable: true),
                    BayesAverage = table.Column<double>(type: "double precision", nullable: true),
                    UsersRated = table.Column<int>(type: "integer", nullable: true),
                    MinPlayers = table.Column<int>(type: "integer", nullable: true),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: true),
                    BestPlayerCounts = table.Column<int[]>(type: "integer[]", nullable: false),
                    RecommendedPlayerCounts = table.Column<int[]>(
                        type: "integer[]",
                        nullable: false
                    ),
                    PlayingTime = table.Column<int>(type: "integer", nullable: true),
                    MinPlayTime = table.Column<int>(type: "integer", nullable: true),
                    MaxPlayTime = table.Column<int>(type: "integer", nullable: true),
                    MinAge = table.Column<int>(type: "integer", nullable: true),
                    ComplexityWeight = table.Column<double>(
                        type: "double precision",
                        nullable: true
                    ),
                    Categories = table.Column<string[]>(type: "text[]", nullable: false),
                    Mechanics = table.Column<string[]>(type: "text[]", nullable: false),
                    GameTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    DetailsUpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    LastImportAttemptAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    ImportError = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.BggId);
                }
            );

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "InteractionEvents",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "bigint", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    SessionId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    BggId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteractionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InteractionEvents_Games_BggId",
                        column: x => x.BggId,
                        principalTable: "Games",
                        principalColumn: "BggId",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_InteractionEvents_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Interactions",
                columns: table => new
                {
                    SessionId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    BggId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interactions", x => new { x.SessionId, x.BggId });
                    table.ForeignKey(
                        name: "FK_Interactions_Games_BggId",
                        column: x => x.BggId,
                        principalTable: "Games",
                        principalColumn: "BggId",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Interactions_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Games_AverageRating",
                table: "Games",
                column: "AverageRating"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Games_BggRank",
                table: "Games",
                column: "BggRank"
            );

            migrationBuilder
                .CreateIndex(name: "IX_Games_Categories", table: "Games", column: "Categories")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Games_DetailsUpdatedAt",
                table: "Games",
                column: "DetailsUpdatedAt"
            );

            migrationBuilder
                .CreateIndex(name: "IX_Games_Mechanics", table: "Games", column: "Mechanics")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder
                .CreateIndex(name: "IX_Games_Name", table: "Games", column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_YearPublished",
                table: "Games",
                column: "YearPublished"
            );

            migrationBuilder.CreateIndex(
                name: "IX_InteractionEvents_BggId",
                table: "InteractionEvents",
                column: "BggId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_InteractionEvents_SessionId_CreatedAt",
                table: "InteractionEvents",
                columns: new[] { "SessionId", "CreatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_BggId",
                table: "Interactions",
                column: "BggId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_SessionId_Action",
                table: "Interactions",
                columns: new[] { "SessionId", "Action" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InteractionEvents");

            migrationBuilder.DropTable(name: "Interactions");

            migrationBuilder.DropTable(name: "Games");

            migrationBuilder.DropTable(name: "Sessions");
        }
    }
}
