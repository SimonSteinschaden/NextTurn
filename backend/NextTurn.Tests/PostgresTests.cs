using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NextTurn.Api.Data;
using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;
using NextTurn.Api.Services;
using Npgsql;
using Xunit;

namespace NextTurn.Tests;

public class PostgresFixture : IAsyncLifetime
{
    private string adminConnection = "";
    private readonly string database = "nextturn_test_" + Guid.NewGuid().ToString("N");
    public string Connection { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets("9cdbb92b-7d86-4a3b-b461-1880a0cee753")
            .AddEnvironmentVariables()
            .Build();
        var original =
            Environment.GetEnvironmentVariable("NEXTTURN_TEST_CONNECTION")
            ?? config.GetConnectionString("NextTurn")
            ?? throw new InvalidOperationException(
                "Integration tests require local User Secrets ConnectionStrings:NextTurn or NEXTTURN_TEST_CONNECTION."
            );
        var builder = new NpgsqlConnectionStringBuilder(original)
        {
            Database = "postgres",
            Pooling = false,
        };
        adminConnection = builder.ConnectionString;
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand("CREATE DATABASE " + database, admin);
        await create.ExecuteNonQueryAsync();
        builder.Database = database;
        Connection = builder.ConnectionString;
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }

    public NextTurnDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<NextTurnDbContext>().UseNpgsql(Connection).Options);

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(adminConnection))
            return;
        if (!database.StartsWith("nextturn_test_") || database.Length != 46)
            throw new InvalidOperationException("Refusing to drop an unexpected database.");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand(
            "DROP DATABASE IF EXISTS " + database + " WITH (FORCE)",
            admin
        );
        await drop.ExecuteNonQueryAsync();
    }
}

[Trait("Category", "Integration")]
public class PostgresTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static BoardGameRankEntry Rank(int id, int rank, string name) =>
        new()
        {
            BggId = id,
            BggRank = rank,
            Name = name,
            YearPublished = 2020,
            AverageRating = 7.5,
        };

    private static BoardGameDto Detail(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTime = 30,
            MaxPlayTime = 60,
            PlayingTime = 60,
            Complexity = 2.5,
            Categories = ["Fantasy"],
            Mechanics = ["Deck Building"],
            GameTypes = ["boardgame"],
            AverageRating = 8,
        };

    [Fact]
    public async Task ImportUpsertsResumesAndPreservesMetadataOnFailure()
    {
        await using var db = fixture.CreateDb();
        var repo = new EfGameRepository(db);
        var ranks = new[] { Rank(1001, 1, "Import One"), Rank(1002, 2, "Import Two") };
        await repo.SeedAsync(ranks, default);
        await repo.SeedAsync(ranks, default);
        Assert.Equal(2, await db.Games.CountAsync(g => g.BggId == 1001 || g.BggId == 1002));
        await repo.SaveDetailsAsync([Detail(1001, "Import One")], [1001], null, default);
        var pending = await repo.ImportIdsAsync(DateTimeOffset.UtcNow.AddDays(-30), default);
        Assert.DoesNotContain(1001, pending);
        Assert.Contains(1002, pending);
        await repo.SaveDetailsAsync([], [1001], "Temporary upstream error", default);
        var saved = await repo.FindAsync(1001, default);
        Assert.NotNull(saved!.DetailsUpdatedAt);
        Assert.Equal("Fantasy", saved.Categories.Single());
        await repo.SaveDetailsAsync([Detail(1002, "Import Two")], [1002], null, default);
        await using var restarted = fixture.CreateDb();
        Assert.Equal(
            2,
            await restarted.Games.CountAsync(g =>
                (g.BggId == 1001 || g.BggId == 1002) && g.DetailsUpdatedAt != null
            )
        );
    }

    [Fact]
    public async Task LocalSearchFiltersSortsPaginatesAndEscapesWildcard()
    {
        await using var db = fixture.CreateDb();
        var repo = new EfGameRepository(db);
        await repo.SeedAsync(
            [
                Rank(2001, 1, "Needle Alpha"),
                Rank(2002, 2, "Needle Beta"),
                Rank(2003, 3, "Different"),
            ],
            default
        );
        await repo.SaveDetailsAsync(
            [Detail(2001, "Needle Alpha"), Detail(2002, "Needle Beta")],
            [2001, 2002],
            null,
            default
        );
        var result = await repo.SearchAsync(
            new()
            {
                Q = "nEEdLE",
                Players = 3,
                MaxMinutes = 60,
                MaxComplexity = 3,
                Category = "Fantasy",
                Mechanic = "Deck Building",
                PageSize = 1,
                Page = 2,
            },
            null,
            false,
            default
        );
        Assert.Equal(2, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(2002, result.Items[0].Id);
        var literal = await repo.SearchAsync(new() { Q = "%" }, null, false, default);
        Assert.Equal(0, literal.Total);
        var facets = await repo.FacetsAsync(default);
        Assert.Contains("Fantasy", facets.Categories);
    }

    [Fact]
    public async Task LikeFromAnyViewHasOneCurrentFavoriteAndDurableHistory()
    {
        await using (var seed = fixture.CreateDb())
        {
            var repo = new EfGameRepository(seed);
            await repo.SeedAsync([Rank(3001, 1, "Interaction Test")], default);
            await repo.SaveDetailsAsync([Detail(3001, "Interaction Test")], [3001], null, default);
        }
        const string session = "integration-session";
        async Task Rate(SwipeAction action)
        {
            await using var db = fixture.CreateDb();
            var games = new EfGameRepository(db);
            var events = new EfInteractionRepository(db);
            await new UserPreferenceService(events, games).ApplySwipeAsync(
                new()
                {
                    SessionId = session,
                    GameId = 3001,
                    Action = action,
                }
            );
        }
        await Rate(SwipeAction.Like);
        await Rate(SwipeAction.Like);
        await Rate(SwipeAction.SuperLike);
        await using (var read = fixture.CreateDb())
        {
            Assert.Equal(1, await read.Interactions.CountAsync(i => i.SessionId == session));
            Assert.Equal(2, await read.InteractionEvents.CountAsync(i => i.SessionId == session));
            var favorites = await new EfGameRepository(read).SearchAsync(
                new(),
                session,
                true,
                default
            );
            Assert.Single(favorites.Items);
            Assert.Equal(SwipeAction.SuperLike, favorites.Items[0].UserAction);
            var candidates = await new EfGameRepository(read).CandidatesAsync(
                session,
                3,
                [],
                [],
                true,
                100,
                default
            );
            Assert.DoesNotContain(candidates, g => g.BggId == 3001);
        }
        await Rate(SwipeAction.Dislike);
        await using (var read = fixture.CreateDb())
        {
            var current = await new EfInteractionRepository(read).CurrentAsync(session, default);
            Assert.Equal(0, StatisticsService.Calculate(current).Favorites);
            Assert.Equal(
                -1,
                UserPreferenceService.BuildProfile(session, current).CategoryScores["Fantasy"]
            );
        }
        await Rate(SwipeAction.Skip);
        await using (var read = fixture.CreateDb())
        {
            var candidates = await new EfGameRepository(read).CandidatesAsync(
                session,
                3,
                [],
                [],
                true,
                100,
                default
            );
            Assert.DoesNotContain(candidates, g => g.BggId == 3001);
        }
    }

    [Fact]
    public async Task ActualCsvProvidesTenThousandUniqueGeneralRanks()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (
            folder != null
            && !File.Exists(Path.Combine(folder.FullName, "data", "boardgames_ranks.csv"))
        )
            folder = folder.Parent;
        Assert.NotNull(folder);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Data:BoardGameRanksCsvPath"] = Path.Combine(
                        folder.FullName,
                        "data",
                        "boardgames_ranks.csv"
                    ),
                }
            )
            .Build();
        var ranks = new CsvGameRepository(config).LoadRankedGames();
        Assert.Equal(10000, ranks.Count);
        Assert.Equal(10000, ranks.Select(g => g.BggId).Distinct().Count());
        await using var db = fixture.CreateDb();
        var repo = new EfGameRepository(db);
        await repo.SeedAsync(ranks, default);
        await repo.SeedAsync(ranks, default);
        var status = await repo.StatusAsync(false, default);
        Assert.Equal(10000, status.Total);
    }

    [Fact]
    public async Task ConcurrentDuplicateLikesStayIdempotent()
    {
        await using (var seed = fixture.CreateDb())
        {
            var games = new EfGameRepository(seed);
            await games.SeedAsync([Rank(4001, 1, "Concurrent Test")], default);
            await games.SaveDetailsAsync([Detail(4001, "Concurrent Test")], [4001], null, default);
        }
        var session = "concurrent-" + Guid.NewGuid().ToString("N");
        await Task.WhenAll(
            Enumerable
                .Range(0, 6)
                .Select(async _ =>
                {
                    await using var db = fixture.CreateDb();
                    await new EfInteractionRepository(db).SaveAsync(
                        session,
                        4001,
                        SwipeAction.Like,
                        default
                    );
                })
        );
        await using var verify = fixture.CreateDb();
        Assert.Equal(1, await verify.Interactions.CountAsync(i => i.SessionId == session));
        Assert.Equal(1, await verify.InteractionEvents.CountAsync(i => i.SessionId == session));
    }

    [Fact]
    public async Task CandidatePoolsKeepBothRankBandsAndAllowNewGenres()
    {
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        await games.SeedAsync(
            [
                Rank(50011, 1, "Rank head favorite"),
                Rank(50012, 500, "Rank head discovery"),
                Rank(50013, 501, "Rank tail favorite"),
                Rank(50014, 9000, "Rank tail discovery"),
            ],
            default
        );
        var headOther = Detail(50012, "Rank head discovery");
        headOther.Categories = ["Party"];
        headOther.Mechanics = ["Dice Rolling"];
        var tailOther = Detail(50014, "Rank tail discovery");
        tailOther.Categories = ["Party"];
        tailOther.Mechanics = ["Dice Rolling"];
        await games.SaveDetailsAsync(
            [
                Detail(50011, "Rank head favorite"),
                headOther,
                Detail(50013, "Rank tail favorite"),
                tailOther,
            ],
            [50011, 50012, 50013, 50014],
            null,
            default
        );
        foreach (var exploration in new[] { false, true })
        {
            var candidates = await games.CandidatesAsync(
                "rank-pool-test",
                3,
                ["Fantasy"],
                ["Deck Building"],
                exploration,
                4,
                default
            );
            Assert.Equal(4, candidates.Count);
            Assert.Equal(2, candidates.Count(g => g.BggRank <= 500));
            Assert.Equal(2, candidates.Count(g => g.BggRank > 500));
            Assert.Contains(candidates, g => g.Categories.Contains("Party"));
        }
        var limited = await games.CandidatesAsync("rank-pool-test", 3, [], [], false, 1, default);
        Assert.Single(limited);
    }

    [Fact]
    public async Task MatchQualityUsesPersistedCurrentFavoritesInAllGameViews()
    {
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        var interactions = new EfInteractionRepository(db);
        await games.SeedAsync(
            [
                Rank(61001, 1, "Match favorite"),
                Rank(61002, 2, "Match superlike"),
                Rank(61003, 3, "Match target"),
            ],
            default
        );
        await games.SaveDetailsAsync(
            [
                Detail(61001, "Match favorite"),
                Detail(61002, "Match superlike"),
                Detail(61003, "Match target"),
            ],
            [61001, 61002, 61003],
            null,
            default
        );
        var session = "match-" + Guid.NewGuid().ToString("N");
        await interactions.SaveAsync(session, 61001, SwipeAction.Like, default);
        await interactions.SaveAsync(session, 61002, SwipeAction.SuperLike, default);
        var matches = new MatchQualityService(interactions);
        var search = await games.SearchAsync(new() { Q = "Match target" }, session, false, default);
        await matches.ApplyAsync(search.Items, session, default);
        var expected = Assert.Single(search.Items).MatchQuality;
        Assert.NotNull(expected);
        Assert.Equal(100, expected.Percentage);
        Assert.Equal(2, expected.ComparedFavorites);
        Assert.True(expected.IsProvisional);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var queue = await new RecommendationService(
            games,
            interactions,
            config
        ).GetRecommendationsAsync(session, 3, [], []);
        Assert.Equal(expected, Assert.Single(queue).MatchQuality);
        var details = GameMapping.ToDto((await games.FindAsync(61003, default))!);
        await matches.ApplyAsync([details], session, default);
        Assert.Equal(expected, details.MatchQuality);
        var favorites = await games.SearchAsync(new(), session, true, default);
        await matches.ApplyAsync(favorites.Items, session, default);
        Assert.All(favorites.Items, g => Assert.Equal(1, g.MatchQuality!.ComparedFavorites));
        await interactions.SaveAsync(session, 61002, SwipeAction.Dislike, default);
        await matches.ApplyAsync([details], session, default);
        Assert.Equal(1, details.MatchQuality!.ComparedFavorites);
        await interactions.SaveAsync(session, 61001, SwipeAction.Skip, default);
        await matches.ApplyAsync([details], session, default);
        Assert.Null(details.MatchQuality!.Percentage);
    }

    [Fact]
    public async Task IndependentGenreMatchesAreConsistentAcrossSearchDiscoveryDetailsAndNight()
    {
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        var interactions = new EfInteractionRepository(db);
        var ids = new[] { 62001, 62002, 62003, 62004 };
        await games.SeedAsync(
            ids.Select((id, index) => Rank(id, index + 1, "Genre profile " + id)).ToArray(),
            default
        );
        var details = ids.Select(id =>
            {
                var g = Detail(id, "Genre profile " + id);
                if (id % 2 == 0)
                {
                    g.Categories = ["Economic"];
                    g.Mechanics = ["Auction"];
                    g.Complexity = 1;
                    g.MinPlayTime = 20;
                    g.MaxPlayTime = 30;
                    g.MinPlayers = 4;
                    g.MaxPlayers = 6;
                }
                return g;
            })
            .ToArray();
        await games.SaveDetailsAsync(details, ids, null, default);
        var session = "genre-" + Guid.NewGuid().ToString("N");
        await interactions.SaveAsync(session, 62001, SwipeAction.Like, default);
        await interactions.SaveAsync(session, 62002, SwipeAction.SuperLike, default);
        var matches = new MatchQualityService(interactions);
        var search = await games.SearchAsync(
            new() { Q = "Genre profile" },
            session,
            false,
            default
        );
        await matches.ApplyAsync(search.Items, session, default);
        var targets = search.Items.Where(g => g.Id is 62003 or 62004).ToDictionary(g => g.Id);
        Assert.Equal(100, targets[62003].MatchQuality!.Percentage);
        Assert.Equal(100, targets[62004].MatchQuality!.Percentage);
        Assert.Equal("Fantasy", targets[62003].MatchQuality!.MatchedGenre);
        Assert.Equal("Economic", targets[62004].MatchQuality!.MatchedGenre);
        foreach (var id in new[] { 62003, 62004 })
        {
            var dto = GameMapping.ToDto((await games.FindAsync(id, default))!);
            await matches.ApplyAsync([dto], session, default);
            Assert.Equal(targets[id].MatchQuality, dto.MatchQuality);
            await interactions.SetOwnershipAsync(session, id, true, default);
        }
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var queue = await new RecommendationService(
            games,
            interactions,
            config
        ).GetRecommendationsAsync(session, 4, [], []);
        Assert.Equal(2, queue.Count);
        Assert.All(queue, g => Assert.Equal(targets[g.Id].MatchQuality, g.MatchQuality));
        var nights = new GameNightService(new EfGameNightRepository(db), matches);
        var plan = await nights.CreateAsync(session, new() { Players = 4 }, default);
        var board = await nights.BoardAsync(plan.Id, session, 1, default);
        Assert.Equal(2, board.Candidates.Count);
        Assert.All(board.Candidates, g => Assert.Equal(targets[g.Id].MatchQuality, g.MatchQuality));
        var selected = await nights.ChooseAsync(plan.Id, session, 62003, SwipeAction.Like, default);
        Assert.Equal(
            targets[62003].MatchQuality,
            Assert.Single(selected.Selections.Items).Game.MatchQuality
        );
        await interactions.SaveAsync(session, 62001, SwipeAction.Skip, default);
        var changed = await matches.LoadAsync(session, default);
        var fantasy = MatchQualityService.Calculate(targets[62003], changed);
        var economic = MatchQualityService.Calculate(targets[62004], changed);
        Assert.Null(fantasy.MatchedGenre);
        Assert.Equal("Economic", economic.MatchedGenre);
        Assert.Equal(100, economic.Percentage);
    }

    [Fact]
    public async Task ProfileUsesPersistedCommunityBestAndDurationDistributionAfterChangedFavorites()
    {
        await using var db = fixture.CreateDb();
        var games = new EfGameRepository(db);
        var interactions = new EfInteractionRepository(db);
        await games.SeedAsync(
            [
                Rank(63001, 1, "Profile quick"),
                Rank(63002, 2, "Profile long"),
                Rank(63003, 3, "Only owned"),
            ],
            default
        );
        var quick = Detail(63001, "Profile quick");
        quick.MinPlayers = 1;
        quick.MaxPlayers = 9;
        quick.BestPlayerCounts = [3];
        quick.RecommendedPlayerCounts = [2, 3, 4];
        quick.MinPlayTime = 20;
        quick.MaxPlayTime = 30;
        var slow = Detail(63002, "Profile long");
        slow.MinPlayers = 2;
        slow.MaxPlayers = 6;
        slow.BestPlayerCounts = [3];
        slow.MinPlayTime = 120;
        slow.MaxPlayTime = 180;
        await games.SaveDetailsAsync(
            [quick, slow, Detail(63003, "Only owned")],
            [63001, 63002, 63003],
            null,
            default
        );
        var session = "profile-modes-" + Guid.NewGuid().ToString("N");
        await interactions.SaveAsync(session, 63001, SwipeAction.Like, default);
        await interactions.SaveAsync(session, 63002, SwipeAction.SuperLike, default);
        await interactions.SetOwnershipAsync(session, 63003, true, default);
        await using var restarted = fixture.CreateDb();
        var service = new StatisticsService(new EfInteractionRepository(restarted));
        var result = await service.GetAsync(session, default);
        Assert.Equal(2, result.Favorites);
        Assert.Equal([3], result.PreferredPlayers.Counts);
        Assert.Equal(2, result.PreferredPlayers.KnownGames);
        Assert.Equal(100, result.PreferredPlayers.Percentage);
        Assert.Equal("mixed", result.PlayTimePreference.Tendency);
        Assert.Equal(1, result.PlayTimePreference.ShortGames);
        Assert.Equal(1, result.PlayTimePreference.LongGames);
        Assert.Equal(0, result.PlayTimePreference.MissingGames);
        await interactions.SaveAsync(session, 63001, SwipeAction.Skip, default);
        result = await service.GetAsync(session, default);
        Assert.Equal("long", result.PlayTimePreference.Tendency);
        Assert.Equal([3], result.PreferredPlayers.Counts);
        Assert.Equal(1, result.PreferredPlayers.KnownGames);
    }
}
