using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

public class RankWeightingTests
{
    private static Game Game(
        int id,
        int? rank,
        string category = "Fantasy",
        string mechanic = "Deck Building"
    ) =>
        new()
        {
            BggId = id,
            BggRank = rank,
            Name = "Rank test " + id,
            Categories = [category],
            Mechanics = [mechanic],
            MinPlayers = 2,
            MaxPlayers = 4,
            AverageRating = 8,
            DetailsUpdatedAt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public void FirstProposalUsesTopFiveHundredInAboutEightyPercentOfDraws()
    {
        var preferred = new[] { Game(3, 501), Game(4, 10000) };
        var exploration = new[] { Game(1, 1, "Other"), Game(2, 500, "Other") };
        var profile = new UserPreferenceProfile
        {
            TotalSwipes = 1000,
            CategoryScores = new() { ["Fantasy"] = 1000 },
        };
        var random = new Random(20261027);
        var top = 0;
        const int draws = 20000;
        for (var i = 0; i < draws; i++)
        {
            var game = Assert.Single(
                RecommendationService.SelectRecommendations(
                    preferred,
                    exploration,
                    profile,
                    3,
                    1,
                    0,
                    random: random
                )
            );
            if (game.BggRank <= 500)
                top++;
        }
        var share = top / (double)draws;
        Assert.InRange(share, 0.79, 0.81);
    }

    [Fact]
    public void WorseRanksWithinTheSameFavoriteGenreGetProgressivelyFewerSelections()
    {
        var pool = new[] { Game(1, 1), Game(2, 250), Game(3, 500) };
        var profile = new UserPreferenceProfile
        {
            CategoryScores = new() { ["Fantasy"] = 5 },
            MechanicScores = new() { ["Deck Building"] = 3 },
        };
        var random = new Random(7251);
        var counts = new int[4];
        for (var i = 0; i < 20000; i++)
        {
            var game = Assert.Single(
                RecommendationService.SelectRecommendations(
                    pool,
                    pool,
                    profile,
                    3,
                    1,
                    0,
                    topRankProbability: 1,
                    random: random
                )
            );
            counts[game.Id]++;
        }
        Assert.True(counts[1] > counts[2], string.Join(",", counts));
        Assert.True(counts[2] > counts[3], string.Join(",", counts));
        Assert.True(counts[3] > 0, "A worse-ranked game must remain possible.");
    }

    [Fact]
    public void PreferredGenresStillHaveMoreChanceAtEqualRank()
    {
        var pool = new[] { Game(1, 100, "Fantasy"), Game(2, 100, "Economic") };
        var profile = new UserPreferenceProfile { CategoryScores = new() { ["Fantasy"] = 4 } };
        var random = new Random(823);
        var liked = 0;
        for (var i = 0; i < 5000; i++)
            if (
                RecommendationService
                    .SelectRecommendations(pool, pool, profile, 3, 1, 0, random: random)[0]
                    .Id == 1
            )
                liked++;
        Assert.InRange(liked / 5000d, .65, .8);
    }

    [Fact]
    public void PreferredMechanicsStillHaveMoreChanceAtEqualRank()
    {
        var pool = new[]
        {
            Game(1, 100, mechanic: "Worker Placement"),
            Game(2, 100, mechanic: "Dice Rolling"),
        };
        var profile = new UserPreferenceProfile
        {
            MechanicScores = new() { ["Worker Placement"] = 4 },
        };
        var random = new Random(824);
        var liked = 0;
        for (var i = 0; i < 5000; i++)
            if (
                RecommendationService
                    .SelectRecommendations(pool, pool, profile, 3, 1, 0, random: random)[0]
                    .Id == 1
            )
                liked++;
        Assert.InRange(liked / 5000d, .62, .78);
    }

    [Fact]
    public void DiscoveryShareAndRankBandShareRemainSeparate()
    {
        var preferred = Enumerable.Range(1, 40).Select(i => Game(i, i)).ToList();
        var exploration = Enumerable.Range(101, 40).Select(i => Game(i, i, "Party")).ToList();
        var profile = new UserPreferenceProfile { CategoryScores = new() { ["Fantasy"] = 5 } };
        var result = RecommendationService.SelectRecommendations(
            preferred,
            exploration,
            profile,
            3,
            20,
            .2,
            random: new Random(10)
        );
        Assert.Equal(4, result.Count(g => g.RecommendationReason == "Neue Richtung entdecken"));
        Assert.Equal(4, result.Count(g => g.Categories.Contains("Party")));
        Assert.Equal(20, result.Select(g => g.Id).Distinct().Count());
    }

    [Fact]
    public void ExhaustedTopBandFallsBackWithoutReturningRatedGames()
    {
        var pool = Enumerable.Range(501, 30).Select(i => Game(i, i)).Prepend(Game(1, 1)).ToList();
        var profile = new UserPreferenceProfile { RatedGameIds = [1] };
        var result = RecommendationService.SelectRecommendations(
            pool,
            pool,
            profile,
            3,
            20,
            .2,
            random: new Random(3)
        );
        Assert.Equal(20, result.Count);
        Assert.All(result, g => Assert.True(g.BggRank > 500));
        Assert.Equal(20, result.Select(g => g.Id).Distinct().Count());
    }

    [Fact]
    public void MissingRanksAreExcludedAndSmallPoolsStayUnique()
    {
        var pool = new[] { Game(1, null), Game(2, 10) };
        var result = RecommendationService.SelectRecommendations(
            pool,
            pool,
            new(),
            3,
            20,
            .2,
            random: new Random(9)
        );
        Assert.Equal(2, Assert.Single(result).Id);
        Assert.Empty(RecommendationService.SelectRecommendations(pool, pool, new(), 3, 0, .2));
    }

    [Fact]
    public void NextCardKeepsBothProbabilitiesWhenQueueIsRerankedAfterEverySwipe()
    {
        var preferred = Enumerable
            .Range(1, 20)
            .Select(i => Game(i, i))
            .Concat(Enumerable.Range(501, 20).Select(i => Game(i, i)))
            .ToList();
        var explore = Enumerable
            .Range(101, 20)
            .Select(i => Game(i, i, "Party"))
            .Concat(Enumerable.Range(1001, 20).Select(i => Game(i, i, "Party")))
            .ToList();
        var profile = new UserPreferenceProfile { CategoryScores = new() { ["Fantasy"] = 5 } };
        var random = new Random(8301);
        var firstDiscoveries = 0;
        var firstTop = 0;
        for (var i = 0; i < 2000; i++)
        {
            var queue = RecommendationService.SelectRecommendations(
                preferred,
                explore,
                profile,
                3,
                20,
                .2,
                random: random
            );
            Assert.Equal(4, queue.Count(g => g.RecommendationReason == "Neue Richtung entdecken"));
            if (queue[0].RecommendationReason == "Neue Richtung entdecken")
                firstDiscoveries++;
            if (queue[0].BggRank <= 500)
                firstTop++;
        }
        Assert.InRange(firstDiscoveries / 2000d, .17, .23);
        Assert.InRange(firstTop / 2000d, .77, .83);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(499)]
    [InlineData(500)]
    [InlineData(501)]
    [InlineData(10000)]
    public void ExplicitRankLimitIsInclusiveAndNeverWidenedByBandFallback(int limit)
    {
        var pool = new[]
        {
            Game(1, 1),
            Game(2, 500),
            Game(3, 501),
            Game(4, 10000),
            Game(5, null),
            Game(6, 0),
        };
        var profile = new UserPreferenceProfile
        {
            RatedGameIds = [1],
            CategoryScores = new() { ["Fantasy"] = 10000 },
        };
        var result = RecommendationService.SelectRecommendations(
            pool,
            pool,
            profile,
            3,
            20,
            .2,
            topRankProbability: 0,
            random: new Random(17),
            maxRank: limit
        );
        var expected = pool.Where(g => g.BggId != 1 && g.BggRank > 0 && g.BggRank <= limit)
            .Select(g => g.BggId)
            .Order()
            .ToArray();
        Assert.Equal(expected, result.Select(g => g.Id).Order().ToArray());
        Assert.All(result, g => Assert.InRange(g.BggRank!.Value, 1, limit));
    }
}
