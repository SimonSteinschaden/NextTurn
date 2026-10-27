using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

public class RecommendationTests
{
    private static Game Game(int id, string category = "Strategy", int min = 2, int max = 4) =>
        new()
        {
            BggId = id,
            BggRank = id,
            Name = "Test " + id,
            Categories = [category],
            Mechanics = ["Deck Building"],
            MinPlayers = min,
            MaxPlayers = max,
            ComplexityWeight = 3,
            PlayingTime = 90,
            AverageRating = 7,
            DetailsUpdatedAt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public void RatedGamesNeverReturnEvenIfPresentInBothPools()
    {
        var pool = Enumerable.Range(1, 40).Select(i => Game(i)).ToList();
        var profile = new UserPreferenceProfile
        {
            RatedGameIds = [1, 2, 3, 4, 5, 6],
            CategoryScores = new() { ["Strategy"] = 10 },
        };
        var result = RecommendationService.SelectRecommendations(pool, pool, profile, 3, 20, .2);
        Assert.Equal(20, result.Count);
        Assert.Equal(20, result.Select(g => g.Id).Distinct().Count());
        Assert.DoesNotContain(result, g => profile.RatedGameIds.Contains(g.Id));
    }

    [Fact]
    public void ReservesTwentyPercentForUnfamiliarGenres()
    {
        var preferred = Enumerable.Range(1, 40).Select(i => Game(i)).ToList();
        var explore = Enumerable.Range(101, 40).Select(i => Game(i, "Party")).ToList();
        var profile = new UserPreferenceProfile
        {
            TotalSwipes = 10,
            CategoryScores = new() { ["Strategy"] = 100 },
            MechanicScores = new() { ["Deck Building"] = 50 },
        };
        var result = RecommendationService.SelectRecommendations(
            preferred,
            explore,
            profile,
            3,
            20,
            .2
        );
        Assert.Equal(16, result.Count(g => g.Categories.Contains("Strategy")));
        Assert.Equal(4, result.Count(g => g.Categories.Contains("Party")));
        Assert.Equal(4, result.Count(g => g.RecommendationReason == "Neue Richtung entdecken"));
    }

    [Fact]
    public void FiltersUnsupportedPlayersAndMissingDetails()
    {
        var unknown = Game(3);
        unknown.DetailsUpdatedAt = null;
        var pool = new List<Game> { Game(1, "Strategy", 1, 1), Game(2, "Party", 2, 6), unknown };
        var result = RecommendationService.SelectRecommendations(pool, pool, new(), 4, 20, .2);
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
    }

    [Fact]
    public void EmptyOrExhaustedCatalogReturnsEmptyWithoutRepeats()
    {
        Assert.Empty(RecommendationService.SelectRecommendations([], [], new(), null, 20, .2));
        Assert.Empty(
            RecommendationService.SelectRecommendations(
                [Game(1)],
                [Game(1)],
                new() { RatedGameIds = [1] },
                null,
                20,
                .2
            )
        );
    }

    [Fact]
    public void LikesAndSuperlikesLearnButDislikesDoNotSetPreferredComplexity()
    {
        var disliked = Game(3);
        disliked.ComplexityWeight = 5;
        var first = Game(1);
        first.ComplexityWeight = 2;
        var second = Game(2);
        second.ComplexityWeight = 4;
        var current = new List<GameInteraction>
        {
            new()
            {
                BggId = 1,
                Action = SwipeAction.Like,
                Game = first,
            },
            new()
            {
                BggId = 2,
                Action = SwipeAction.SuperLike,
                Game = second,
            },
            new()
            {
                BggId = 3,
                Action = SwipeAction.Dislike,
                Game = disliked,
            },
            new()
            {
                BggId = 4,
                Action = SwipeAction.Skip,
                Game = Game(4),
            },
        };
        var profile = UserPreferenceService.BuildProfile("test", current);
        Assert.Equal(3, profile.CategoryScores["Strategy"]);
        Assert.Equal(3.5, profile.PreferredComplexity);
        Assert.Equal(3, profile.TotalSwipes);
        Assert.Equal(4, profile.RatedGameIds.Length);
    }

    [Fact]
    public void ChangedRatingRebuildsLearningFromCurrentState()
    {
        var profile = UserPreferenceService.BuildProfile(
            "test",
            [
                new()
                {
                    BggId = 1,
                    Action = SwipeAction.Dislike,
                    Game = Game(1),
                },
            ]
        );
        Assert.Equal(-1, profile.CategoryScores["Strategy"]);
        Assert.Equal(0, profile.PreferredComplexity);
    }
}
