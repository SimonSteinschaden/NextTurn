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
        var other = Favorite(2, "Fantasy", "Dice Rolling");
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
        // Same-genre comparisons score 100% and 50%; the SuperLike counts threefold.
        Assert.Equal(88, MatchQualityService.Calculate(Candidate(), context).Percentage);
        var reversed = MatchQualityService.BuildContext([
            Rated(liked),
            Rated(other, SwipeAction.SuperLike),
        ]);
        Assert.Equal(63, MatchQualityService.Calculate(Candidate(), reversed).Percentage);
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
    public void AdditionalGenresDoNotDiluteKnownGenreAndPartialMechanicsRemainBounded()
    {
        var candidate = Candidate();
        candidate.Categories = ["Fantasy", "Economic"];
        candidate.Mechanics = ["Worker Placement", "Dice Rolling"];
        var result = MatchQualityService.Calculate(
            candidate,
            MatchQualityService.BuildContext([Rated(Favorite(1))])
        );
        // A matching genre supplies 35%; mechanics still use cosine, numeric/range data 30%.
        Assert.Equal(90, result.Percentage);
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

    [Fact]
    public void FantasyAndEconomicCanBothBePerfectMatchesDespiteDifferentPlayStyles()
    {
        var fantasy = Favorite(1, "Fantasy", "Deck Building");
        fantasy.ComplexityWeight = 4;
        fantasy.MinPlayTime = 120;
        fantasy.MaxPlayTime = 180;
        fantasy.MinPlayers = 1;
        fantasy.MaxPlayers = 3;
        var economic = Favorite(2, "Economic", "Auction");
        economic.ComplexityWeight = 2;
        economic.MinPlayTime = 30;
        economic.MaxPlayTime = 60;
        economic.MinPlayers = 4;
        economic.MaxPlayers = 6;
        var context = MatchQualityService.BuildContext([Rated(fantasy), Rated(economic)]);
        fantasy.BggId = 101;
        economic.BggId = 102;
        var fantasyMatch = MatchQualityService.Calculate(GameMapping.ToDto(fantasy), context);
        var economicMatch = MatchQualityService.Calculate(GameMapping.ToDto(economic), context);
        Assert.Equal(100, fantasyMatch.Percentage);
        Assert.Equal(100, economicMatch.Percentage);
        Assert.Equal("Fantasy", fantasyMatch.MatchedGenre);
        Assert.Equal("Economic", economicMatch.MatchedGenre);
        Assert.Equal(1, fantasyMatch.ComparedFavorites);
        Assert.Equal(1, economicMatch.ComparedFavorites);
    }

    [Fact]
    public void ManySuperlikedGamesInAnotherGenreNeitherDiluteNorCertifyFantasy()
    {
        var fantasy = Rated(Favorite(1));
        var others = Enumerable
            .Range(2, 30)
            .Select(id =>
            {
                var g = Favorite(id, "Economic", "Dice Rolling");
                g.ComplexityWeight = 1;
                g.MinPlayTime = 10;
                g.MaxPlayTime = 20;
                g.MinPlayers = 6;
                g.MaxPlayers = 8;
                return Rated(g, SwipeAction.SuperLike);
            });
        var before = MatchQualityService.Calculate(
            Candidate(),
            MatchQualityService.BuildContext([fantasy])
        );
        var after = MatchQualityService.Calculate(
            Candidate(),
            MatchQualityService.BuildContext(others.Prepend(fantasy))
        );
        Assert.Equal(before, after);
        Assert.Equal(100, after.Percentage);
        Assert.True(after.IsProvisional);
        Assert.Equal(1, after.AvailableFavorites);
        Assert.Equal(1, after.ComparedFavorites);
    }

    [Fact]
    public void MultiGenreGameUsesBestApplicableProfileRatherThanAnAverage()
    {
        var economic = Favorite(2, "Economic", "Dice Rolling");
        economic.ComplexityWeight = 1;
        var context = MatchQualityService.BuildContext([Rated(Favorite(1)), Rated(economic)]);
        var candidate = Candidate();
        candidate.Categories = ["Economic", "Fantasy"];
        var result = MatchQualityService.Calculate(candidate, context);
        Assert.Equal(100, result.Percentage);
        Assert.Equal("Fantasy", result.MatchedGenre);
        candidate.Categories.Reverse();
        Assert.Equal(result, MatchQualityService.Calculate(candidate, context));
    }

    [Fact]
    public void MatchingGenreDoesNotHidePoorMechanicsComplexityOrDuration()
    {
        var candidate = Candidate();
        candidate.Mechanics = ["Dice Rolling"];
        candidate.Complexity = 5;
        candidate.MinPlayTime = 450;
        candidate.MaxPlayTime = 510;
        candidate.MinPlayers = 6;
        candidate.MaxPlayers = 8;
        var result = MatchQualityService.Calculate(
            candidate,
            MatchQualityService.BuildContext([Rated(Favorite(1))])
        );
        Assert.Equal("Fantasy", result.MatchedGenre);
        Assert.InRange(result.Percentage!.Value, 35, 60);
        Assert.Equal(100, result.FeatureCoveragePercentage);
    }

    [Fact]
    public void UnfamiliarGenreKeepsItsThirtyFivePercentWeightAndAllowsOtherMatches()
    {
        var candidate = Candidate();
        candidate.Categories = ["Economic"];
        var result = MatchQualityService.Calculate(
            candidate,
            MatchQualityService.BuildContext([Rated(Favorite(1))])
        );
        Assert.Equal(65, result.Percentage);
        Assert.Null(result.MatchedGenre);
        Assert.Equal(1, result.ComparedFavorites);
    }

    [Fact]
    public void MissingGenreMetadataStillUsesKnownFeaturesWithoutInventingAProfile()
    {
        var favorite = Favorite(1);
        favorite.Categories = [];
        var result = MatchQualityService.Calculate(
            Candidate(),
            MatchQualityService.BuildContext([Rated(favorite)])
        );
        Assert.Equal(100, result.Percentage);
        Assert.Null(result.MatchedGenre);
        Assert.Equal(65, result.FeatureCoveragePercentage);
        Assert.True(result.IsProvisional);
    }

    [Fact]
    public void GenreProfilesIgnoreCaseDuplicateTagsAndAdditionalGenresOnFavorites()
    {
        var favorite = Favorite(1);
        favorite.Categories = [" Fantasy ", "FANTASY", "Economic"];
        var candidate = Candidate();
        candidate.Categories = ["fantasy", "Fantasy", "Economic"];
        var result = MatchQualityService.Calculate(
            candidate,
            MatchQualityService.BuildContext([Rated(favorite), Rated(favorite)])
        );
        Assert.Equal(100, result.Percentage);
        Assert.Equal(1, result.ComparedFavorites);
        Assert.Equal(1, result.AvailableFavorites);
    }

    [Fact]
    public void OwnLikeCannotCreateAGenreProfileWhenOnlyUnrelatedFavoritesRemain()
    {
        var candidate = Candidate(1);
        var context = MatchQualityService.BuildContext([
            Rated(Favorite(1)),
            Rated(Favorite(2, "Economic")),
        ]);
        var result = MatchQualityService.Calculate(candidate, context);
        Assert.Null(result.MatchedGenre);
        Assert.Equal(65, result.Percentage);
        Assert.Equal(1, result.AvailableFavorites);
    }

    [Fact]
    public void ConfidenceRequiresFiveReferencesInTheMatchingGenre()
    {
        var context = MatchQualityService.BuildContext(
            Enumerable
                .Range(1, 5)
                .Select(id => Rated(Favorite(id)))
                .Append(Rated(Favorite(6, "Economic", "Dice Rolling")))
        );
        var result = MatchQualityService.Calculate(Candidate(), context);
        Assert.Equal(100, result.Percentage);
        Assert.Equal(5, result.ComparedFavorites);
        Assert.Equal(5, result.AvailableFavorites);
        Assert.False(result.IsProvisional);
    }
}
