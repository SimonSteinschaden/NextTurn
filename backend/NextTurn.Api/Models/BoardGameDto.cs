namespace NextTurn.Api.Models;

public class BoardGameDto
{
    public int Id { get; set; }
    public int? BggRank { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Thumbnail { get; set; } = "";
    public string Image { get; set; } = "";
    public int? YearPublished { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public List<PlayerCountPollDto> PlayerCountPoll { get; set; } = [];
    public int[] BestPlayerCounts { get; set; } = [];
    public int[] RecommendedPlayerCounts { get; set; } = [];
    public int? PlayingTime { get; set; }
    public int? MinPlayTime { get; set; }
    public int? MaxPlayTime { get; set; }
    public int? MinAge { get; set; }
    public double? AverageRating { get; set; }
    public double? BayesAverage { get; set; }
    public int? UsersRated { get; set; }
    public double? Complexity { get; set; }
    public GameMatchQuality? MatchQuality { get; set; }
    public double RecommendationScore { get; set; }
    public string? RecommendationReason { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<string> Mechanics { get; set; } = [];
    public string[] GameTypes { get; set; } = [];
    public bool DetailsAvailable { get; set; }
    public SwipeAction? UserAction { get; set; }
}

public class PlayerCountPollDto
{
    public string PlayerCount { get; set; } = "";
    public int Best { get; set; }
    public int Recommended { get; set; }
    public int NotRecommended { get; set; }
    public bool IsRecommended => Best + Recommended > NotRecommended;
}
