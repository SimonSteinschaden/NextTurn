using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

[Trait("Category", "Integration")]
public class RankingFilterTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static string Session() => "rank-filter-" + Guid.NewGuid().ToString("N");

    private static readonly int[] Ids = [95001, 95002, 95003, 95004, 95005, 95006];

    private async Task SeedAsync()
    {
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        var ranks = new[] { 1, 500, 501, 9000, 10, 20 };
        await games.SeedAsync(
            Ids.Select(
                    (id, index) =>
                        new BoardGameRankEntry
                        {
                            BggId = id,
                            BggRank = ranks[index],
                            Name = "Rank boundary " + id,
                        }
                )
                .ToArray(),
            default
        );
        await games.SaveDetailsAsync(
            Ids.Select(id => new BoardGameDto
                {
                    Id = id,
                    Name = "Rank boundary " + id,
                    MinPlayers = 2,
                    MaxPlayers = 4,
                    Categories = ["Fantasy"],
                    Mechanics = ["Deck Building"],
                })
                .ToArray(),
            Ids,
            null,
            default
        );
        await db
            .Games.Where(g => g.BggId == 95005)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.BggRank, (int?)null));
        await db
            .Games.Where(g => g.BggId == 95006)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.BggRank, (int?)0));
    }

    [Fact]
    public async Task RankFilterPrecedesSearchPaginationAndPreservesHiddenFavoritesAndOwnership()
    {
        await SeedAsync();
        var session = Session();
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        var interactions = new EfInteractionRepository(db);
        foreach (var id in Ids)
        {
            await interactions.SetOwnershipAsync(session, id, true, default);
            await interactions.SaveAsync(session, id, SwipeAction.Like, default);
        }
        foreach (var favorites in new[] { false, true })
        {
            var page = await games.SearchAsync(
                new()
                {
                    Q = "Rank boundary",
                    MaxRank = 500,
                    Page = 2,
                    PageSize = 1,
                },
                session,
                favorites,
                default
            );
            Assert.Equal(2, page.Total);
            Assert.Equal(95002, Assert.Single(page.Items).Id);
            var combined = await games.SearchAsync(
                new()
                {
                    MaxRank = 501,
                    Players = 3,
                    Category = "Fantasy",
                    OwnedOnly = true,
                    Sort = "name",
                },
                session,
                favorites,
                default
            );
            Assert.Equal(new[] { 95001, 95002, 95003 }, combined.Items.Select(g => g.Id));
        }
        var owned = await games.SearchAsync(
            new() { OwnedOnly = true, MaxRank = 499 },
            session,
            false,
            default
        );
        Assert.Equal(95001, Assert.Single(owned.Items).Id);
        var allOwned = await games.SearchAsync(new() { OwnedOnly = true }, session, false, default);
        Assert.Equal(6, allOwned.Total);
        Assert.Equal(6, (await games.SearchAsync(new(), session, true, default)).Total);
        Assert.Equal(6, await db.InteractionEvents.CountAsync(i => i.SessionId == session));
        Assert.Equal(6, await db.Interactions.CountAsync(i => i.SessionId == session && i.IsOwned));
    }

    [Fact]
    public async Task CandidatePoolsAndDiscoveryApplyLimitBeforeSamplingAndStayEmptyWhenExhausted()
    {
        await SeedAsync();
        var session = Session();
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        var interactions = new EfInteractionRepository(db);
        foreach (var exploration in new[] { true, false })
        {
            var pool = await games.CandidatesAsync(
                session,
                3,
                ["Fantasy"],
                [],
                exploration,
                300,
                default,
                maxRank: 500
            );
            Assert.Equal(new[] { 95001, 95002 }, pool.Select(g => g.BggId).Order());
        }
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var service = new RecommendationService(games, interactions, config);
        var queue = await service.GetRecommendationsAsync(session, 3, [], [], maxRank: 500);
        Assert.Equal(new[] { 95001, 95002 }, queue.Select(g => g.Id).Order());
        await interactions.SaveAsync(session, 95001, SwipeAction.Like, default);
        Assert.Empty(await service.GetRecommendationsAsync(session, 3, [], [], maxRank: 1));
        queue = await service.GetRecommendationsAsync(session, 3, [], []);
        Assert.Equal(new[] { 95002, 95003, 95004 }, queue.Select(g => g.Id).Order());
    }

    [Fact]
    public async Task NightFilterAppliesToCandidatesAndShortlistWithoutDeletingSavedDecisions()
    {
        await SeedAsync();
        var session = Session();
        await using var db = fixture.CreateDb();
        var interactions = new EfInteractionRepository(db);
        foreach (var id in Ids)
            await interactions.SetOwnershipAsync(session, id, true, default);
        var nights = new GameNightService(
            new EfGameNightRepository(db),
            new MatchQualityService(interactions)
        );
        var plan = await nights.CreateAsync(session, new(), default);
        var limited = await nights.BoardAsync(plan.Id, session, 1, default, 500);
        Assert.Equal(new[] { 95001, 95002 }, limited.Candidates.Select(g => g.Id).Order());
        Assert.Equal(2, limited.RemainingCount);
        await nights.ChooseAsync(plan.Id, session, 95003, SwipeAction.Like, default);
        limited = await nights.ChooseAsync(
            plan.Id,
            session,
            95001,
            SwipeAction.SuperLike,
            default,
            500
        );
        Assert.Equal(95001, Assert.Single(limited.Selections.Items).Game.Id);
        Assert.Equal(95002, Assert.Single(limited.Candidates).Id);
        Assert.Equal(1, limited.Selections.Total);
        var unfiltered = await nights.BoardAsync(plan.Id, session, 1, default);
        Assert.Equal(2, unfiltered.Selections.Total);
        Assert.Equal(4, unfiltered.Candidates.Count);
        limited = await nights.UpdateAsync(
            plan.Id,
            session,
            new() { IncludeUnowned = true },
            default,
            500
        );
        Assert.Equal(95002, Assert.Single(limited.Candidates).Id);
        Assert.Equal(95001, Assert.Single(limited.Selections.Items).Game.Id);
        Assert.Equal(2, await db.GameNightChoices.CountAsync(c => c.GameNightId == plan.Id));
        Assert.Equal(0, await db.InteractionEvents.CountAsync(i => i.SessionId == session));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            nights.BoardAsync(plan.Id, session, 1, default, 0)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            nights.UpdateAsync(plan.Id, session, new(), default, 10001)
        );
    }
}
