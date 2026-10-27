using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

[Trait("Category", "Integration")]
public class DesignerPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static BoardGameDto Detail(int id, params string[] names) =>
        new()
        {
            Id = id,
            Name = "Designer game " + id,
            Designers = names,
            Categories = ["Fantasy"],
            Mechanics = ["Deck Building"],
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTime = 60,
            Complexity = 3,
        };

    [Fact]
    public async Task DesignersPersistAcrossEveryGameViewAndInfluenceNightOrdering()
    {
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        var interactions = new EfInteractionRepository(db);
        await games.SeedAsync(
            [
                new()
                {
                    BggId = 97001,
                    BggRank = 1,
                    Name = "Reference",
                },
                new()
                {
                    BggId = 97002,
                    BggRank = 900,
                    Name = "Known designer",
                },
                new()
                {
                    BggId = 97003,
                    BggRank = 2,
                    Name = "Other designer",
                },
            ],
            default
        );
        await games.SaveDetailsAsync(
            [Detail(97001, "Ada"), Detail(97002, " Ada ", "ADA"), Detail(97003, "Ben")],
            [97001, 97002, 97003],
            null,
            default
        );
        var session = "designer-" + Guid.NewGuid().ToString("N");
        await interactions.SaveAsync(session, 97001, SwipeAction.Like, default);
        foreach (var id in new[] { 97002, 97003 })
            await interactions.SetOwnershipAsync(session, id, true, default);
        await using var restarted = fixture.CreateDb();
        var loadedGames = new EfGameRepository(restarted);
        var loadedInteractions = new EfInteractionRepository(restarted);
        Assert.Equal(
            ["Ada"],
            GameMapping.ToDto((await loadedGames.FindAsync(97002, default))!).Designers
        );
        Assert.Equal(
            ["Ada"],
            (await loadedGames.SearchAsync(new(), session, true, default)).Items.Single().Designers
        );
        Assert.Equal(
            2,
            (
                await loadedGames.SearchAsync(new() { OwnedOnly = true }, session, false, default)
            ).Total
        );
        var profile = await new UserPreferenceService(
            loadedInteractions,
            loadedGames
        ).GetProfileAsync(session);
        Assert.Equal(1, profile.DesignerScores["Ada"]);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var queue = await new RecommendationService(
            loadedGames,
            loadedInteractions,
            config
        ).GetRecommendationsAsync(session, 3, [], []);
        Assert.Equal(["Ada"], queue.Single(g => g.Id == 97002).Designers);
        Assert.Equal(
            10,
            queue.Single(g => g.Id == 97002).RecommendationScore
                - queue.Single(g => g.Id == 97003).RecommendationScore
        );
        var stats = await new StatisticsService(loadedInteractions).GetAsync(session, default);
        Assert.Equal("Ada", Assert.Single(stats.Designers).Name);
        var nights = new GameNightService(
            new EfGameNightRepository(restarted),
            new MatchQualityService(loadedInteractions)
        );
        var plan = await nights.CreateAsync(session, new(), default);
        var board = await nights.BoardAsync(plan.Id, session, 1, default);
        Assert.Equal(97002, board.Candidates[0].Id);
        Assert.Equal(100, board.Candidates[0].MatchQuality!.Percentage);
        var selected = await nights.ChooseAsync(plan.Id, session, 97002, SwipeAction.Like, default);
        Assert.Equal(["Ada"], Assert.Single(selected.Selections.Items).Game.Designers);
        Assert.DoesNotContain(
            97002,
            await loadedGames.ImportIdsAsync(DateTimeOffset.UtcNow.AddDays(-30), default)
        );
    }

    [Fact]
    public async Task CreatorOnlyPreferenceShapesBoundedPersonalAndDiscoveryPools()
    {
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        var ids = Enumerable.Range(98001, 80).ToArray();
        await games.SeedAsync(
            ids.Select(
                    (id, index) =>
                        new BoardGameRankEntry
                        {
                            BggId = id,
                            BggRank = index + 1,
                            Name = "Author pool " + id,
                        }
                )
                .ToArray(),
            default
        );
        await games.SaveDetailsAsync(
            ids.Select(id => Detail(id, id < 98041 ? "Ada" : "Ben")).ToArray(),
            ids,
            null,
            default
        );
        var personal = await games.CandidatesAsync(
            "pool",
            3,
            [],
            [],
            false,
            1,
            default,
            preferredDesigners: ["Ada"]
        );
        var discovery = await games.CandidatesAsync(
            "pool",
            3,
            [],
            [],
            true,
            1,
            default,
            preferredDesigners: ["Ada"]
        );
        Assert.Equal(["Ada"], Assert.Single(personal).Designers);
        Assert.Equal(["Ben"], Assert.Single(discovery).Designers);
        // The author signal must still distinguish candidates when every game matches the same genre.
        for (var sample = 0; sample < 5; sample++)
        {
            var familiarGenre = await games.CandidatesAsync(
                "pool",
                3,
                ["Fantasy"],
                [],
                false,
                1,
                default,
                preferredDesigners: ["Ada"]
            );
            var newAuthor = await games.CandidatesAsync(
                "pool",
                3,
                ["Fantasy"],
                [],
                true,
                1,
                default,
                preferredDesigners: ["Ada"]
            );
            Assert.Equal(["Ada"], Assert.Single(familiarGenre).Designers);
            Assert.Equal(["Ben"], Assert.Single(newAuthor).Designers);
        }

        var limited = await games.CandidatesAsync(
            "pool",
            3,
            [],
            [],
            true,
            1,
            default,
            maxRank: 1,
            preferredDesigners: ["Ada"]
        );
        Assert.Equal(1, Assert.Single(limited).BggRank);
    }
}
