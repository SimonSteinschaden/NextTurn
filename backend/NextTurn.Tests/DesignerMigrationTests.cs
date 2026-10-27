using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;
using Xunit;

namespace NextTurn.Tests;

[Trait("Category", "Integration")]
public class DesignerMigrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task UpgradePreservesMetadataFavoritesAndOwnershipAndBackfillResumesAfterFailure()
    {
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        await games.SeedAsync(
            [
                new BoardGameRankEntry
                {
                    BggId = 99001,
                    BggRank = 1,
                    Name = "Existing",
                },
            ],
            default
        );
        var detail = new BoardGameDto
        {
            Id = 99001,
            Name = "Existing",
            Categories = ["Fantasy"],
            MinPlayers = 2,
            MaxPlayers = 4,
        };
        await games.SaveDetailsAsync([detail], [99001], null, default);
        const string session = "before-designer-upgrade";
        var interactions = new EfInteractionRepository(db);
        await interactions.SaveAsync(session, 99001, SwipeAction.SuperLike, default);
        await interactions.SetOwnershipAsync(session, 99001, true, default);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261027144309_OwnershipAndGameNights");
        await migrator.MigrateAsync();
        await using var restarted = fixture.CreateDb();
        var repo = new EfGameRepository(restarted);
        var saved = (await repo.FindAsync(99001, default))!;
        Assert.Empty(saved.Designers);
        Assert.Null(saved.DesignersUpdatedAt);
        Assert.NotNull(saved.DetailsUpdatedAt);
        Assert.Equal(["Fantasy"], saved.Categories);
        var current = Assert.Single(
            await new EfInteractionRepository(restarted).CurrentAsync(session, default)
        );
        Assert.True(current.IsOwned);
        Assert.Equal(SwipeAction.SuperLike, current.Action);
        Assert.Equal(1, await restarted.InteractionEvents.CountAsync(e => e.SessionId == session));
        Assert.Contains(
            99001,
            await repo.ImportIdsAsync(DateTimeOffset.UtcNow.AddDays(-30), default)
        );
        await repo.SaveDetailsAsync([], [99001], "Temporary upstream failure", default);
        Assert.Contains(
            99001,
            await repo.ImportIdsAsync(DateTimeOffset.UtcNow.AddDays(-30), default)
        );
        Assert.NotNull((await repo.FindAsync(99001, default))!.DetailsUpdatedAt);
        // A successful response with no credited names is complete and must not retry forever.
        await repo.SaveDetailsAsync([detail], [99001], null, default);
        Assert.NotNull((await repo.FindAsync(99001, default))!.DesignersUpdatedAt);
        Assert.DoesNotContain(
            99001,
            await repo.ImportIdsAsync(DateTimeOffset.UtcNow.AddDays(-30), default)
        );
        Assert.False(restarted.Database.HasPendingModelChanges());
    }
}
