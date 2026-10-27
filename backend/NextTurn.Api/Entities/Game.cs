namespace NextTurn.Api.Entities;

public class Game
{
    public int BggId { get; set; }
    public string Name { get; set; } = "";
    public int? YearPublished { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int? BggRank { get; set; }
    public double? AverageRating { get; set; }
    public double? BayesAverage { get; set; }
    public int? UsersRated { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public int[] BestPlayerCounts { get; set; } = [];
    public int[] RecommendedPlayerCounts { get; set; } = [];
    public int? PlayingTime { get; set; }
    public int? MinPlayTime { get; set; }
    public int? MaxPlayTime { get; set; }
    public int? MinAge { get; set; }
    public double? ComplexityWeight { get; set; }
    public string[] Categories { get; set; } = [];
    public string[] Mechanics { get; set; } = [];
    public string[] GameTypes { get; set; } = [];
    public DateTimeOffset? DetailsUpdatedAt { get; set; }
    public DateTimeOffset? LastImportAttemptAt { get; set; }
    public string? ImportError { get; set; }
}
