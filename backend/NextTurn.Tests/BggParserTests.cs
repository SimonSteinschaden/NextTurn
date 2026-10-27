using NextTurn.Api.Services;
using Xunit;

namespace NextTurn.Tests;

public class BggParserTests
{
    [Fact]
    public void PreservesDistinctRankRatingTypesAndParsesMetadata()
    {
        var games = BggService.ParseBoardGamesXml(
            """
            <items><item type="boardgame" id="42"><name type="primary" value="Test"/>
            <description>Example &amp; description</description><minplayers value="2"/><maxplayers value="4"/>
            <minplaytime value="30"/><maxplaytime value="90"/><minage value="12"/>
            <link type="boardgamecategory" value="Fantasy"/><link type="boardgamemechanic" value="Worker Placement"/>
            <statistics><ratings><average value="8.4"/><bayesaverage value="7.2"/><usersrated value="123"/><averageweight value="3.1"/>
            <ranks><rank name="strategygames" value="1"/><rank name="boardgame" value="25"/></ranks></ratings></statistics>
            <poll name="suggested_numplayers"><results numplayers="3"><result value="Best" numvotes="10"/><result value="Recommended" numvotes="5"/><result value="Not Recommended" numvotes="1"/></results></poll>
            </item><item type="boardgameexpansion" id="43"><name type="primary" value="Expansion"/></item></items>
            """
        );
        Assert.Equal(2, games.Count);
        var game = games[0];
        Assert.Equal(25, game.BggRank);
        Assert.Equal(8.4, game.AverageRating);
        Assert.Equal(7.2, game.BayesAverage);
        Assert.Equal(["Fantasy"], game.Categories);
        Assert.Equal(["Worker Placement"], game.Mechanics);
        Assert.Equal(["boardgame"], game.GameTypes);
        Assert.Equal([3], game.BestPlayerCounts);
        Assert.Equal([3], game.RecommendedPlayerCounts);
        Assert.Equal(12, game.MinAge);
        Assert.Equal(123, game.UsersRated);
        Assert.Equal(["boardgameexpansion"], games[1].GameTypes);
        Assert.Null(games[1].Complexity);
        Assert.Null(games[1].AverageRating);
    }

    [Fact]
    public void ZeroPlaceholdersAreUnknownButAncientPublicationYearsRemainValid()
    {
        var game = Assert.Single(
            BggService.ParseBoardGamesXml(
                "<items><item type='boardgame' id='42'><name type='primary' value='Ancient'/><yearpublished value='-2200'/><minplayers value='0'/><playingtime value='0'/><minage value='0'/></item></items>"
            )
        );
        Assert.Equal(-2200, game.YearPublished);
        Assert.Null(game.MinPlayers);
        Assert.Null(game.PlayingTime);
        Assert.Null(game.MinAge);
    }
}
