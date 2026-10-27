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
        Assert.Equal(20, result.AverageRank);
        Assert.Equal(8, result.AverageRating);
        Assert.Equal(60, result.AveragePlayTime);
        Assert.Equal(100, result.Categories.Single(c => c.Name == "Fantasy").Percentage);
        Assert.Equal(50, result.Categories.Single(c => c.Name == "Strategy").Percentage);
        Assert.Equal(1, result.MissingData["Komplexität"]);
        Assert.Equal(3, result.PlayerCounts.Count);
        Assert.Single(result.RecommendedPlayerCounts);
        Assert.Contains("wenigen", result.Profile);
    }

    [Fact]
    public void EmptyProfileDoesNotInventAverages()
    {
        var result = StatisticsService.Calculate([]);
        Assert.Equal(0, result.Favorites);
        Assert.Null(result.AverageRank);
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
        Assert.Null(result.AveragePlayTime);
        Assert.Null(result.AverageComplexity);
        Assert.Empty(result.PlayerCounts);
        Assert.Equal(1, result.MissingData["Spieldauer"]);
        Assert.Equal(1, result.MissingData["Spieleranzahl"]);
    }
}
