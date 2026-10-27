using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;
using Xunit;

namespace NextTurn.Tests;

[Trait("Category", "Integration")]
public class OwnershipMigrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task UpgradePreservesExistingFavoriteHistoryAndCatalogWithoutAssumingOwnership()
    {
        // This fixture owns a fresh random test database; never downgrade the user's database.
        await using var db = fixture.CreateDb();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261027125016_InitialCatalog");
        await new EfGameRepository(db).SeedAsync(
            [
                new BoardGameRankEntry
                {
                    BggId = 89001,
                    BggRank = 1,
                    Name = "Before ownership",
                },
            ],
            default
        );
        const string session = "before-ownership";
        var time = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Sessions" ("Id", "CreatedAt") VALUES ({session}, {time});
            INSERT INTO "Interactions" ("SessionId", "BggId", "Action", "UpdatedAt") VALUES ({session}, {89001}, {2}, {time});
            INSERT INTO "InteractionEvents" ("SessionId", "BggId", "Action", "CreatedAt") VALUES ({session}, {89001}, {2}, {time});
            """
        );
        await migrator.MigrateAsync();
        await using var restarted = fixture.CreateDb();
        var current = await new EfInteractionRepository(restarted).CurrentAsync(session, default);
        var row = Assert.Single(current);
        Assert.Equal(SwipeAction.SuperLike, row.Action);
        Assert.False(row.IsOwned);
        Assert.Equal(time, row.UpdatedAt, TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, await restarted.InteractionEvents.CountAsync(e => e.SessionId == session));
        Assert.Equal("Before ownership", (await restarted.Games.SingleAsync()).Name);
        Assert.Empty(await restarted.GameNights.ToListAsync());
        Assert.False(restarted.Database.HasPendingModelChanges());
    }
}
