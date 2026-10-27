using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;

namespace NextTurn.Api.Services;

public class StatisticsService(IInteractionRepository interactions)
{
    public async Task<PlayerStatistics> GetAsync(string sessionId, CancellationToken ct) =>
        Calculate(await interactions.CurrentAsync(sessionId, ct));

    public static double? Duration(Game g) =>
        g.MinPlayTime > 0 && g.MaxPlayTime > 0 ? (g.MinPlayTime.Value + g.MaxPlayTime.Value) / 2d
        : g.PlayingTime > 0 ? g.PlayingTime
        : null;

    private static IEnumerable<int> BestCounts(Game g) =>
        g.BestPlayerCounts.Where(n => n is >= 1 and <= 100).Distinct();

    private static string? DurationClass(Game g)
    {
        if (g.MinPlayTime > 0 && g.MaxPlayTime >= g.MinPlayTime)
            return g.MaxPlayTime <= 60 ? "short"
                : g.MinPlayTime > 60 ? "long"
                : "variable";
        return g.PlayingTime > 0
            ? g.PlayingTime <= 60
                ? "short"
                : "long"
            : null;
    }

    private static DurationPreference DescribeDuration(IReadOnlyList<Game> games)
    {
        var classes = games.Select(DurationClass).Where(c => c != null).ToList();
        var known = classes.Count;
        var quick = classes.Count(c => c == "short");
        var slow = classes.Count(c => c == "long");
        var variable = classes.Count(c => c == "variable");
        // Test raw counts, not rounded display percentages, at the 60% threshold.
        var tendency =
            known == 0 ? "unknown"
            : quick * 100L >= known * 60L ? "short"
            : slow * 100L >= known * 60L ? "long"
            : variable * 100L >= known * 60L ? "variable"
            : "mixed";
        double? Share(int count) => known == 0 ? null : Math.Round(100d * count / known, 1);
        return new(
            tendency,
            known,
            games.Count - known,
            quick,
            slow,
            variable,
            Share(quick),
            Share(slow),
            Share(variable),
            known < 5 || known < games.Count * .7
        );
    }

    public static PlayerStatistics Calculate(IReadOnlyList<GameInteraction> interactions)
    {
        var favorites = interactions
            .Where(i => i.Action is SwipeAction.Like or SwipeAction.SuperLike)
            .DistinctBy(i => i.BggId)
            .ToList();
        var games = favorites.Select(i => i.Game).ToList();
        var count = games.Count;
        double? Average(Func<Game, double?> selector)
        {
            var values = games.Select(selector).OfType<double>().ToArray();
            return values.Length == 0 ? null : Math.Round(values.Average(), 2);
        }
        List<DistributionItem> Distribution(Func<Game, IEnumerable<string>> selector) =>
            games
                .SelectMany(g => selector(g).Distinct())
                .GroupBy(s => s)
                .Select(g => new DistributionItem(
                    g.Key,
                    g.Count(),
                    Math.Round(100d * g.Count() / Math.Max(1, count), 1)
                ))
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Name)
                .ToList();
        List<DistributionItem> PlayerDistribution(Func<Game, IEnumerable<int>> selector) =>
            Distribution(g =>
                    selector(g)
                        .Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture))
                )
                .OrderByDescending(d => d.Count)
                .ThenBy(d => int.Parse(d.Name, System.Globalization.CultureInfo.InvariantCulture))
                .ToList();

        var bestCounts = PlayerDistribution(BestCounts);
        var knownBest = games.Count(g => BestCounts(g).Any());
        var mostCommon = bestCounts.FirstOrDefault()?.Count ?? 0;
        var preferred = new PlayerCountPreference(
            bestCounts
                .Where(d => d.Count == mostCommon)
                .Select(d => int.Parse(d.Name, System.Globalization.CultureInfo.InvariantCulture))
                .Order()
                .ToArray(),
            knownBest,
            mostCommon,
            knownBest == 0 ? null : Math.Round(100d * mostCommon / knownBest, 1),
            knownBest < 5 || knownBest < count * .7
        );
        var duration = DescribeDuration(games);
        var result = new PlayerStatistics
        {
            Favorites = count,
            Likes = favorites.Count(i => i.Action == SwipeAction.Like),
            SuperLikes = favorites.Count(i => i.Action == SwipeAction.SuperLike),
            AverageRating = Average(g => g.AverageRating),
            AverageComplexity = Average(g => g.ComplexityWeight > 0 ? g.ComplexityWeight : null),
            PreferredPlayers = preferred,
            PlayTimePreference = duration,
            Categories = Distribution(g => g.Categories),
            Mechanics = Distribution(g => g.Mechanics),
            BestPlayerCounts = bestCounts,
            RecommendedPlayerCounts = PlayerDistribution(g =>
                g.RecommendedPlayerCounts.Where(n => n is >= 1 and <= 100)
            ),
            Complexity = Distribution(g =>
                g.ComplexityWeight is > 0 and var w
                    ?
                    [
                        w < 2 ? "Leicht (<2)"
                        : w < 3 ? "Mittel (2–3)"
                        : w < 4 ? "Anspruchsvoll (3–4)"
                        : "Sehr komplex (4–5)",
                    ]
                    : []
            ),
            PlayTimes = Distribution(g =>
                DurationClass(g) switch
                {
                    "short" => ["Schnell (bis 60 Min.)"],
                    "long" => ["Lang (über 60 Min.)"],
                    "variable" => ["Variabel (überlappt 60 Min.)"],
                    _ => [],
                }
            ),
            MissingData = new()
            {
                ["Bewertung"] = games.Count(g => !g.AverageRating.HasValue),
                ["Spieldauer"] = duration.MissingGames,
                ["Komplexität"] = games.Count(g => !(g.ComplexityWeight > 0)),
                ["Kategorien"] = games.Count(g => g.Categories.Length == 0),
                ["Mechaniken"] = games.Count(g => g.Mechanics.Length == 0),
                ["Community-Bestbesetzung"] = count - knownBest,
                ["BGG-Spielerempfehlung"] = games.Count(g =>
                    !g.RecommendedPlayerCounts.Any(n => n is >= 1 and <= 100)
                ),
            },
        };
        result.Profile = count switch
        {
            0 =>
                "Dein Spielerprofil entsteht mit deinen ersten Favoriten. Entdecke Spiele und markiere, was dir gefällt.",
            < 5 =>
                "Erste Tendenzen aus wenigen Favoriten – für ein belastbareres Profil fehlen noch weitere Bewertungen.",
            _ => "Häufig in deinen Favoriten: "
                + (
                    result.Categories.Count > 0
                        ? string.Join(", ", result.Categories.Take(3).Select(c => c.Name))
                        : "noch keine Kategorien bekannt"
                )
                + ". Beliebte Mechaniken: "
                + (
                    result.Mechanics.Count > 0
                        ? string.Join(", ", result.Mechanics.Take(3).Select(c => c.Name))
                        : "noch keine Daten"
                )
                + (
                    result.AverageComplexity is { } complexity
                        ? ". Durchschnittliche Komplexität: "
                            + complexity.ToString(
                                "0.0",
                                System.Globalization.CultureInfo.GetCultureInfo("de-DE")
                            )
                            + "/5."
                        : "."
                ),
        };
        if (count > 0)
        {
            result.Profile += duration.Tendency switch
            {
                "short" => " Bei der Spieldauer überwiegen schnelle Spiele bis 60 Minuten.",
                "long" => " Bei der Spieldauer überwiegen lange Spiele über 60 Minuten.",
                "variable" => " Deine Favoriten haben überwiegend variable Zeitspannen.",
                "mixed" => " Bei der Spieldauer zeigt sich ein gemischtes Bild.",
                _ => "",
            };
            if (preferred.Counts.Length is > 0 and <= 3)
                result.Profile +=
                    " Häufig beste Community-Besetzung: "
                    + string.Join(" oder ", preferred.Counts)
                    + (
                        preferred.Counts.Length == 1 && preferred.Counts[0] == 1
                            ? " Person."
                            : " Personen."
                    );
            else if (preferred.Counts.Length > 3)
                result.Profile += " Mehrere Community-Bestbesetzungen liegen gleichauf.";
        }
        return result;
    }
}
