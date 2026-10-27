using System.ComponentModel.DataAnnotations;

namespace NextTurn.Api.Models;

public class CatalogQuery
{
    [StringLength(200)]
    public string? Q { get; set; }

    [Range(1, 10000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 24;

    [RegularExpression("^(rank|rating|name|year)$")]
    public string Sort { get; set; } = "rank";

    [Range(1, 100)]
    public int? Players { get; set; }

    [Range(1, 10000)]
    public int? MaxRank { get; set; }

    [Range(1, 10000)]
    public int? MaxMinutes { get; set; }

    [Range(0, 5)]
    public double? MinComplexity { get; set; }

    [Range(0, 5)]
    public double? MaxComplexity { get; set; }

    [StringLength(150)]
    public string? Category { get; set; }

    [StringLength(150)]
    public string? Mechanic { get; set; }
    public bool OwnedOnly { get; set; }
    public bool SuperOnly { get; set; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record CatalogStatus(
    int Total,
    int Detailed,
    int Failed,
    int Pending,
    bool ImportRunning,
    int DesignersDetailed = 0,
    int DesignersPending = 0
);

public record Facets(string[] Categories, string[] Mechanics);

public record DistributionItem(string Name, int Count, double Percentage);

public class PlayerStatistics
{
    public int Favorites { get; set; }
    public int Likes { get; set; }
    public int SuperLikes { get; set; }
    public double? AverageRating { get; set; }
    public double? AverageComplexity { get; set; }
    public Dictionary<string, int> MissingData { get; set; } = new();
    public List<DistributionItem> Categories { get; set; } = [];
    public List<DistributionItem> Mechanics { get; set; } = [];
    public List<DistributionItem> Designers { get; set; } = [];
    public List<DistributionItem> Complexity { get; set; } = [];
    public List<DistributionItem> BestPlayerCounts { get; set; } = [];
    public PlayerCountPreference PreferredPlayers { get; set; } = new([], 0, 0, null, true);
    public DurationPreference PlayTimePreference { get; set; } =
        new("unknown", 0, 0, 0, 0, 0, null, null, null, true);
    public List<DistributionItem> RecommendedPlayerCounts { get; set; } = [];
    public List<DistributionItem> PlayTimes { get; set; } = [];
    public string Profile { get; set; } = "";
    public string PercentageDenominator { get; set; } =
        "Alle aktuellen Favoriten, jedes Spiel einmal; Mehrfachzuordnung möglich.";
}

public sealed record PlayerCountPreference(
    int[] Counts,
    int KnownGames,
    int MatchingGames,
    double? Percentage,
    bool IsProvisional
);

public sealed record DurationPreference(
    string Tendency,
    int KnownGames,
    int MissingGames,
    int ShortGames,
    int LongGames,
    int VariableGames,
    double? ShortPercentage,
    double? LongPercentage,
    double? VariablePercentage,
    bool IsProvisional
);
