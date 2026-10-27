namespace NextTurn.Api.Models;

// A descriptive similarity index, not a calibrated probability of a future Like.
public sealed record GameMatchQuality(
    double? Percentage,
    int ComparedFavorites,
    int AvailableFavorites,
    double FeatureCoveragePercentage,
    bool IsProvisional
);
