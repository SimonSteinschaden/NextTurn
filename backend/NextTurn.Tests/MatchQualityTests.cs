using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

public class MatchQualityTests
{
    private static Game Favorite(
        int id,
        string category = "Fantasy",
        string mechanic = "Worker Placement"
    ) =>
        new()
        {
            BggId = id,
            Categories = [category],
            Mechanics = [mechanic],
            ComplexityWeight = 3,
            MinPlayTime = 60,
            MaxPlayTime = 120,
            PlayingTime = 120,
            MinPlayers = 2,
            MaxPlayers = 4,
        };

    private static GameInteraction Rated(Game game, SwipeAction action = SwipeAction.Like) =>
        new()
        {
            BggId = game.BggId,
            Game = game,
            Action = action,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static BoardGameDto Candidate(int id = 100) => GameMapping.ToDto(Favorite(id));

    [Fact]
    public void NoFavoritesDoesNotInventAPercentage()
    {
        var result = MatchQualityService.Calculate(
            Candidate(),
            MatchQualityService.BuildContext([])
        );
        Assert.Null(result.Percentage);
        Assert.Equal(0, result.ComparedFavorites);
    }

    [Fact]
    public void SameFeaturesAcrossFiveFavoritesGiveFullSimilarity()
    {
        var context = MatchQualityService.BuildContext(
            Enumerable.Range(1, 5).Select(i => Rated(Favorite(i)))
        );
        var result = MatchQualityService.Calculate(Candidate(), context);
        Assert.Equal(100, result.Percentage);
        Assert.Equal(5, result.ComparedFavorites);
        Assert.Equal(100, result.FeatureCoveragePercentage);
        Assert.False(result.IsProvisional);
    }

    [Fact]
    public void OneReferenceIsProvisionalAndCannotMatchItself()
    {
        var context = MatchQualityService.BuildContext([Rated(Favorite(1))]);
        Assert.Null(MatchQualityService.Calculate(Candidate(1), context).Percentage);
        var other = MatchQualityService.Calculate(Candidate(), context);
        Assert.Equal(100, other.Percentage);
        Assert.True(other.IsProvisional);
    }

    [Fact]
    public void RankAndCommunityRatingDoNotInflatePersonalSimilarity()
    {
        var context = MatchQualityService.BuildContext([Rated(Favorite(1))]);
        var high = Candidate();
        high.BggRank = 1;
        high.AverageRating = 10;
        var low = Candidate();
        low.BggRank = 10000;
        low.AverageRating = 1;
        Assert.Equal(
            MatchQualityService.Calculate(high, context),
            MatchQualityService.Calculate(low, context)
        );
    }

    [Fact]
    public void SuperlikesWeightTheMeanThreeTimesAsStrongly()
    {
        var liked = Favorite(1, "Fantasy", "Worker Placement");
        liked.ComplexityWeight = null;
        liked.MinPlayTime = null;
        liked.MaxPlayTime = null;
        liked.PlayingTime = null;
        liked.MinPlayers = null;
        liked.MaxPlayers = null;
        var other = Favorite(2, "Economic", "Dice Rolling");
        other.ComplexityWeight = null;
        other.MinPlayTime = null;
        other.MaxPlayTime = null;
        other.PlayingTime = null;
        other.MinPlayers = null;
        other.MaxPlayers = null;
        var context = MatchQualityService.BuildContext([
            Rated(liked, SwipeAction.SuperLike),
            Rated(other),
        ]);
        Assert.Equal(75, MatchQualityService.Calculate(Candidate(), context).Percentage);
    }

    [Fact]
    public void ChangedDislikesAndSkipsAreNotPositiveReferences()
    {
        var old = Rated(Favorite(1));
        old.UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var changed = Rated(Favorite(1), SwipeAction.Dislike);
        var context = MatchQualityService.BuildContext([
            old,
            changed,
            Rated(Favorite(2), SwipeAction.Skip),
        ]);
        Assert.Null(MatchQualityService.Calculate(Candidate(), context).Percentage);
        Assert.Equal(SwipeAction.Dislike, context.Actions[1]);
    }

    [Fact]
    public void DuplicateFavoritesDoNotMultiplyEvidence()
    {
        var favorite = Rated(Favorite(1));
        var context = MatchQualityService.BuildContext([favorite, favorite]);
        var result = MatchQualityService.Calculate(Candidate(), context);
        Assert.Equal(1, result.ComparedFavorites);
        Assert.True(result.IsProvisional);
    }

    [Fact]
    public void MissingFieldsAreNotSilentlyCountedAsMismatches()
    {
        var favorite = Favorite(1);
        favorite.Categories = [];
        favorite.Mechanics = [];
        favorite.MinPlayers = null;
        favorite.MaxPlayers = null;
        var context = MatchQualityService.BuildContext([Rated(favorite)]);
        // Duration and complexity alone cover only 25%, below the minimum for a number.
        var result = MatchQualityService.Calculate(Candidate(), context);
        Assert.Null(result.Percentage);
        Assert.Equal(1, result.AvailableFavorites);
    }

    [Fact]
    public void SparseComparableMetadataIsExplicitlyProvisional()
    {
        var favorite = Favorite(1);
        favorite.Mechanics = [];
        favorite.ComplexityWeight = null;
        favorite.MinPlayTime = null;
        favorite.MaxPlayTime = null;
        favorite.PlayingTime = null;
        favorite.MinPlayers = null;
        favorite.MaxPlayers = null;
        var result = MatchQualityService.Calculate(
            Candidate(),
            MatchQualityService.BuildContext([Rated(favorite)])
        );
        Assert.Equal(100, result.Percentage);
        Assert.Equal(35, result.FeatureCoveragePercentage);
        Assert.True(result.IsProvisional);
    }

    [Fact]
    public void NoKnownTagOverlapCanProduceARealZero()
    {
        var candidate = Candidate();
        candidate.Categories = ["Economic"];
        candidate.Mechanics = ["Dice Rolling"];
        candidate.Complexity = null;
        candidate.PlayingTime = null;
        candidate.MinPlayTime = null;
        candidate.MaxPlayTime = null;
        candidate.MinPlayers = null;
        candidate.MaxPlayers = null;
        var result = MatchQualityService.Calculate(
            candidate,
            MatchQualityService.BuildContext([Rated(Favorite(1))])
        );
        Assert.Equal(0, result.Percentage);
    }

    [Fact]
    public void PartialTagOverlapUsesCosineAndIsBounded()
    {
        var candidate = Candidate();
        candidate.Categories = ["Fantasy", "Economic"];
        candidate.Mechanics = ["Worker Placement", "Dice Rolling"];
        var result = MatchQualityService.Calculate(
            candidate,
            MatchQualityService.BuildContext([Rated(Favorite(1))])
        );
        // 70% categorical weight * 1/sqrt(2) + 30% identical numeric/range data.
        Assert.Equal(79, result.Percentage);
        Assert.InRange(result.Percentage!.Value, 0, 100);
    }

    [Fact]
    public void MoreSimilarDurationComplexityAndPlayerRangeIncreaseTheIndex()
    {
        var context = MatchQualityService.BuildContext([Rated(Favorite(1))]);
        var close = Candidate();
        var far = Candidate();
        far.Complexity = 5;
        far.MinPlayTime = 180;
        far.MaxPlayTime = 360;
        far.MinPlayers = 6;
        far.MaxPlayers = 10;
        Assert.True(
            MatchQualityService.Calculate(close, context).Percentage
                > MatchQualityService.Calculate(far, context).Percentage
        );
    }
}
