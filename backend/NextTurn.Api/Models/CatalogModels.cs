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

public record CatalogStatus(int Total, int Detailed, int Failed, int Pending, bool ImportRunning);

public record Facets(string[] Categories, string[] Mechanics);

public record DistributionItem(string Name, int Count, double Percentage);

public class PlayerStatistics
{
    public int Favorites { get; set; }
    public int Likes { get; set; }
    public int SuperLikes { get; set; }
    public double? AverageRank { get; set; }
    public double? AverageRating { get; set; }
    public double? AveragePlayTime { get; set; }
    public double? AverageComplexity { get; set; }
    public Dictionary<string, int> MissingData { get; set; } = new();
    public List<DistributionItem> Categories { get; set; } = [];
    public List<DistributionItem> Mechanics { get; set; } = [];
    public List<DistributionItem> Complexity { get; set; } = [];
    public List<DistributionItem> PlayerCounts { get; set; } = [];
    public List<DistributionItem> RecommendedPlayerCounts { get; set; } = [];
    public List<DistributionItem> PlayTimes { get; set; } = [];
    public string Profile { get; set; } = "";
    public string PercentageDenominator { get; set; } =
        "Alle aktuellen Favoriten, jedes Spiel einmal; Mehrfachzuordnung möglich.";
}
