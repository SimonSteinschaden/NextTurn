using NextTurn.Api.Entities;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

public class StatisticsTests
{
    [Fact]
    public void CountsOnlyUniqueCurrentFavoritesWithExplicitDenominator()
    {
        var a = new Game
        {
            BggId = 1,
            Categories = ["Strategy", "Fantasy", "Fantasy"],
            Mechanics = ["Deck Building"],
            BggRank = 10,
            AverageRating = 8,
            ComplexityWeight = 3,
            MinPlayTime = 30,
            MaxPlayTime = 90,
            MinPlayers = 2,
            MaxPlayers = 4,
            RecommendedPlayerCounts = [3],
            BestPlayerCounts = [3],
        };
        var b = new Game
        {
            BggId = 2,
            Categories = ["Fantasy"],
            BggRank = 30,
        };
        var result = StatisticsService.Calculate([
            new()
            {
                BggId = 1,
                Action = SwipeAction.SuperLike,
                Game = a,
            },
            new()
            {
                BggId = 2,
                Action = SwipeAction.Like,
                Game = b,
            },
            new()
            {
                BggId = 2,
                Action = SwipeAction.Like,
                Game = b,
            },
            new()
            {
                BggId = 3,
                Action = SwipeAction.Dislike,
                Game = new(),
            },
            new()
            {
                BggId = 4,
                Action = SwipeAction.Skip,
                Game = new(),
            },
        ]);
        Assert.Equal(2, result.Favorites);
        Assert.Equal(1, result.SuperLikes);
        Assert.Equal(1, result.Likes);
        Assert.Equal([3], result.PreferredPlayers.Counts);
        Assert.Equal(8, result.AverageRating);
        Assert.Equal("variable", result.PlayTimePreference.Tendency);
        Assert.Equal(100, result.Categories.Single(c => c.Name == "Fantasy").Percentage);
        Assert.Equal(50, result.Categories.Single(c => c.Name == "Strategy").Percentage);
        Assert.Equal(1, result.MissingData["Komplexität"]);
        Assert.Single(result.BestPlayerCounts);
        Assert.Single(result.RecommendedPlayerCounts);
        Assert.Contains("wenigen", result.Profile);
    }

    [Fact]
    public void EmptyProfileDoesNotInventAverages()
    {
        var result = StatisticsService.Calculate([]);
        Assert.Equal(0, result.Favorites);
        Assert.Empty(result.PreferredPlayers.Counts);
        Assert.Equal("unknown", result.PlayTimePreference.Tendency);
        Assert.Null(result.AverageComplexity);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public void ZeroPlaceholdersDoNotCreateZeroMinuteOrZeroWeightPreferences()
    {
        var result = StatisticsService.Calculate([
            new()
            {
                BggId = 42,
                Action = SwipeAction.Like,
                Game = new()
                {
                    PlayingTime = 0,
                    MinPlayTime = 0,
                    MaxPlayTime = 0,
                    MinPlayers = 0,
                    MaxPlayers = 0,
                    ComplexityWeight = 0,
                },
            },
        ]);
        Assert.Equal(0, result.PlayTimePreference.KnownGames);
        Assert.Null(result.AverageComplexity);
        Assert.Empty(result.BestPlayerCounts);
        Assert.Equal(1, result.MissingData["Spieldauer"]);
        Assert.Equal(1, result.MissingData["Community-Bestbesetzung"]);
    }

    private static GameInteraction Liked(int id, Game game, bool superLike = false) =>
        new()
        {
            BggId = id,
            Game = game,
            Action = superLike ? SwipeAction.SuperLike : SwipeAction.Like,
        };

    [Theory]
    [InlineData(3, 2, "short")]
    [InlineData(2, 3, "long")]
    [InlineData(2, 2, "mixed")]
    public void DurationUsesTheDistributionInsteadOfAnOverallMean(
        int quick,
        int slow,
        string expected
    )
    {
        var rows = Enumerable
            .Range(1, quick + slow)
            .Select(id => Liked(id, new Game { PlayingTime = id <= quick ? 20 : 180 }))
            .ToList();
        var result = StatisticsService.Calculate(rows);
        Assert.Equal(expected, result.PlayTimePreference.Tendency);
        Assert.Equal(quick, result.PlayTimePreference.ShortGames);
        Assert.Equal(slow, result.PlayTimePreference.LongGames);
        Assert.Equal(quick + slow, result.PlayTimePreference.KnownGames);
        Assert.DoesNotContain("mittlere Spieldauer", result.Profile);
    }

    [Fact]
    public void CrossingDurationRangeRemainsVariableEvenWithALongFallbackTime()
    {
        var rows = Enumerable
            .Range(1, 5)
            .Select(id =>
                Liked(
                    id,
                    new Game
                    {
                        MinPlayTime = 30,
                        MaxPlayTime = 180,
                        PlayingTime = 180,
                    }
                )
            )
            .ToList();
        var result = StatisticsService.Calculate(rows);
        Assert.Equal("variable", result.PlayTimePreference.Tendency);
        Assert.Equal(5, result.PlayTimePreference.VariableGames);
        Assert.Equal(0, result.PlayTimePreference.LongGames);
        Assert.Equal(100, result.PlayTimePreference.VariablePercentage);
        Assert.False(result.PlayTimePreference.IsProvisional);
        Assert.Equal("Variabel (überlappt 60 Min.)", Assert.Single(result.PlayTimes).Name);
    }

    [Fact]
    public void MissingDurationDoesNotVoteForShortOrLongOrManufactureConfidence()
    {
        var rows = Enumerable
            .Range(1, 25)
            .Select(id => Liked(id, new Game { PlayingTime = id <= 5 ? 60 : 0 }))
            .ToList();
        var result = StatisticsService.Calculate(rows);
        Assert.Equal("short", result.PlayTimePreference.Tendency);
        Assert.Equal(5, result.PlayTimePreference.KnownGames);
        Assert.Equal(20, result.PlayTimePreference.MissingGames);
        Assert.Equal(100, result.PlayTimePreference.ShortPercentage);
        Assert.True(result.PlayTimePreference.IsProvisional);
        Assert.Equal(20, Assert.Single(result.PlayTimes).Percentage);
    }

    [Fact]
    public void DurationThresholdUsesCountsBeforeRoundingAndIncludesSixtyMinutes()
    {
        var rows = Enumerable
            .Range(1, 10001)
            .Select(id =>
                Liked(
                    id,
                    new Game
                    {
                        MinPlayTime = id <= 6000 ? 60 : 61,
                        MaxPlayTime = id <= 6000 ? 60 : 61,
                    }
                )
            )
            .ToList();
        var result = StatisticsService.Calculate(rows);
        Assert.Equal(6000, result.PlayTimePreference.ShortGames);
        Assert.Equal(60, result.PlayTimePreference.ShortPercentage);
        Assert.Equal(4001, result.PlayTimePreference.LongGames);
        Assert.Equal("mixed", result.PlayTimePreference.Tendency);
        Assert.Null(StatisticsService.Calculate([]).PlayTimePreference.ShortPercentage);
    }

    [Fact]
    public void PreferredPlayersUseCommunityBestRatherThanSupportedOrRecommendedCounts()
    {
        var result = StatisticsService.Calculate([
            Liked(
                1,
                new Game
                {
                    MinPlayers = 1,
                    MaxPlayers = 9,
                    BestPlayerCounts = [3],
                    RecommendedPlayerCounts = [2, 3, 4],
                }
            ),
        ]);
        Assert.Equal([3], result.PreferredPlayers.Counts);
        Assert.Equal(1, result.PreferredPlayers.KnownGames);
        Assert.Equal(100, result.PreferredPlayers.Percentage);
        Assert.Equal("3", Assert.Single(result.BestPlayerCounts).Name);
        Assert.Equal(3, result.RecommendedPlayerCounts.Count);
    }

    [Fact]
    public void TiedCommunityBestCountsRemainSeparateAndNeverBecomeAnAverage()
    {
        var result = StatisticsService.Calculate([
            Liked(1, new Game { BestPlayerCounts = [2] }),
            Liked(2, new Game { BestPlayerCounts = [4] }, true),
            Liked(
                3,
                new Game
                {
                    MinPlayers = 2,
                    MaxPlayers = 4,
                    RecommendedPlayerCounts = [3],
                }
            ),
        ]);
        Assert.Equal([2, 4], result.PreferredPlayers.Counts);
        Assert.Equal(2, result.PreferredPlayers.KnownGames);
        Assert.Equal(50, result.PreferredPlayers.Percentage);
        Assert.True(result.PreferredPlayers.IsProvisional);
        Assert.Equal(1, result.MissingData["Community-Bestbesetzung"]);
    }

    [Fact]
    public void MissingCommunityBestNeverFallsBackToManufacturersRange()
    {
        var result = StatisticsService.Calculate([
            Liked(
                1,
                new Game
                {
                    MinPlayers = 1,
                    MaxPlayers = 10,
                    RecommendedPlayerCounts = [3, 4],
                }
            ),
        ]);
        Assert.Empty(result.PreferredPlayers.Counts);
        Assert.Null(result.PreferredPlayers.Percentage);
        Assert.Equal(0, result.PreferredPlayers.KnownGames);
        Assert.Empty(result.BestPlayerCounts);
        Assert.Equal(2, result.RecommendedPlayerCounts.Count);
    }

    [Fact]
    public void CommunityModeDeduplicatesPollCountsAndUsesNumericTieOrdering()
    {
        var result = StatisticsService.Calculate([
            Liked(1, new Game { BestPlayerCounts = [-1, 0, 2, 2, 10, 101] }),
            Liked(2, new Game { BestPlayerCounts = [2, 10] }),
        ]);
        Assert.Equal([2, 10], result.PreferredPlayers.Counts);
        Assert.Equal(2, result.PreferredPlayers.KnownGames);
        Assert.Equal(2, result.PreferredPlayers.MatchingGames);
        Assert.Equal(100, result.PreferredPlayers.Percentage);
        Assert.Equal(new[] { "2", "10" }, result.BestPlayerCounts.Select(d => d.Name));
        Assert.All(result.BestPlayerCounts, d => Assert.Equal(2, d.Count));
    }

    [Fact]
    public void PreferredCommunityCountIsTheMostFrequentBestChoice()
    {
        var rows = Enumerable
            .Range(1, 5)
            .Select(id => Liked(id, new Game { BestPlayerCounts = id <= 3 ? [4] : [2] }))
            .ToList();
        var result = StatisticsService.Calculate(rows);
        Assert.Equal([4], result.PreferredPlayers.Counts);
        Assert.Equal(60, result.PreferredPlayers.Percentage);
        Assert.False(result.PreferredPlayers.IsProvisional);
        Assert.DoesNotContain("Ranking", result.MissingData.Keys);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("AverageRank", json);
        Assert.DoesNotContain("AveragePlayTime", json);
    }
}
