using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

public class DesignerTests
{
    private static Game Game(int id, string[] designers, int rank = 1) =>
        new()
        {
            BggId = id,
            BggRank = rank,
            Name = "Designer fixture " + id,
            Designers = designers,
            Categories = ["Fantasy"],
            Mechanics = ["Deck Building"],
            DetailsUpdatedAt = DateTimeOffset.UtcNow,
            MinPlayers = 2,
            MaxPlayers = 4,
            AverageRating = 8,
        };

    private static GameInteraction Vote(int id, string[] names, SwipeAction? action) =>
        new()
        {
            BggId = id,
            Game = Game(id, names),
            Action = action,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public void XmlParserReadsOnlyCreditedDesignersAndDecodesTheirNames()
    {
        var game = Assert.Single(
            BggService.ParseBoardGamesXml(
                """
                <items><item type="boardgame" id="42"><name type="primary" value="Fixture"/>
                <link type="boardgamedesigner" value=" Ada &amp; Ben "/><link type="boardgamedesigner" value="ada &amp; ben"/>
                <link type="boardgamedesigner" value="Other Author"/><link type="boardgamedesigner" value="(Uncredited)"/>
                <link type="boardgameartist" value="Artist"/><link type="boardgamepublisher" value="Publisher"/>
                </item></items>
                """
            )
        );
        Assert.Equal(new[] { "Ada & Ben", "Other Author" }, game.Designers);
        Assert.Empty(DesignerPreference.Names(["", " ", "(Unknown)", "Uncredited"]));
    }

    [Fact]
    public void CurrentVotesLearnDesignersWithoutDuplicatesHistoryOrOwnership()
    {
        var repeated = Vote(2, ["Ada", "Ben", "Ada"], SwipeAction.SuperLike);
        var rows = new[]
        {
            Vote(1, ["Ada"], SwipeAction.Like),
            repeated,
            repeated,
            Vote(3, ["ADA"], SwipeAction.Dislike),
            Vote(4, ["Ben"], SwipeAction.Skip),
            Vote(5, ["Owned only"], null),
        };
        var profile = UserPreferenceService.BuildProfile("test", rows);
        Assert.Equal(3, profile.DesignerScores["ada"]);
        Assert.Equal(3, profile.DesignerScores["Ben"]);
        Assert.DoesNotContain("Owned only", profile.DesignerScores.Keys);
        var changed = Vote(1, ["Ada"], SwipeAction.Skip);
        changed.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        Assert.Empty(DesignerPreference.Learn([rows[0], changed]));
        changed.Action = SwipeAction.Dislike;
        Assert.Equal(-1, DesignerPreference.Learn([rows[0], changed])["Ada"]);
    }

    [Fact]
    public void BonusIsBoundedAndCoauthorsDoNotMultiplyOrDiluteKnownSignals()
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ada"] = 1000,
            ["Ben"] = 1000,
            ["Unliked"] = -1000,
        };
        Assert.Equal(20, DesignerPreference.Score(["Ada"], scores));
        Assert.Equal(20, DesignerPreference.Score(["Ada", "ADA", "Ben"], scores));
        Assert.Equal(20, DesignerPreference.Score(["Ada", "New coauthor"], scores));
        Assert.Equal(-20, DesignerPreference.Score(["Unliked"], scores));
        Assert.Equal(0, DesignerPreference.Score(["Nobody known"], scores));
        Assert.Equal(0, DesignerPreference.Score([], scores));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void FavoriteDesignerIncreasesDrawChanceEvenWithASaturatedGenreProfile(int categoryScore)
    {
        var pool = new[] { Game(1, ["Ada"]), Game(2, ["Other"]) };
        var profile = new UserPreferenceProfile
        {
            DesignerScores = new() { ["Ada"] = 3 },
            CategoryScores = new() { ["Fantasy"] = categoryScore },
        };
        var random = new Random(71314);
        var preferred = 0;
        const int draws = 20000;
        for (var i = 0; i < draws; i++)
            if (
                RecommendationService
                    .SelectRecommendations(pool, pool, profile, 3, 1, 0, random: random)[0]
                    .Id == 1
            )
                preferred++;
        Assert.InRange(preferred / (double)draws, .54, .57);
    }

    [Fact]
    public void DesignerCannotOverrideTopFiveHundredProbabilityOrAnExplicitRankLimit()
    {
        var pool = new[] { Game(1, ["Other"], 1), Game(2, ["Ada"], 501) };
        var profile = new UserPreferenceProfile { DesignerScores = new() { ["Ada"] = 10000 } };
        var random = new Random(20261028);
        var top = 0;
        for (var i = 0; i < 20000; i++)
            if (
                RecommendationService
                    .SelectRecommendations(pool, pool, profile, 3, 1, 0, random: random)[0]
                    .BggRank <= 500
            )
                top++;
        Assert.InRange(top / 20000d, .79, .81);
        var limited = RecommendationService.SelectRecommendations(
            pool,
            pool,
            profile,
            3,
            20,
            .2,
            maxRank: 500
        );
        Assert.Equal(1, Assert.Single(limited).Id);
    }

    [Fact]
    public void DesignerStatisticsUseCurrentFavoritesAndCountEachDesignerOncePerGame()
    {
        var rows = new[]
        {
            Vote(1, ["Ada", "Ada", "Ben"], SwipeAction.Like),
            Vote(2, ["ada"], SwipeAction.SuperLike),
            Vote(3, ["(Unknown)"], SwipeAction.Like),
            Vote(4, ["Ben"], SwipeAction.Dislike),
            Vote(5, ["Owned"], null),
        };
        var stats = StatisticsService.Calculate(rows);
        Assert.Equal(3, stats.Favorites);
        Assert.Equal(2, stats.Designers.Count);
        Assert.Equal(2, stats.Designers.Single(d => d.Name == "Ada").Count);
        Assert.Equal(66.7, stats.Designers.Single(d => d.Name == "Ada").Percentage);
        Assert.Equal(1, stats.Designers.Single(d => d.Name == "Ben").Count);
        Assert.Equal(1, stats.MissingData["Spieldesigner"]);
    }

    [Fact]
    public void DesignerAffectsOrderWithoutChangingTheAgreedMatchQualityIndex()
    {
        var reference = Vote(1, ["Ada"], SwipeAction.Like);
        var context = MatchQualityService.BuildContext([reference]);
        var known = GameMapping.ToDto(Game(2, ["Ada"]));
        var other = GameMapping.ToDto(Game(3, ["Other"]));
        Assert.Equal(
            MatchQualityService.Calculate(known, context),
            MatchQualityService.Calculate(other, context)
        );
        Assert.Equal(10, DesignerPreference.Score(known.Designers, context.DesignerScores));
        Assert.Equal(0, DesignerPreference.Score(other.Designers, context.DesignerScores));
    }
}
