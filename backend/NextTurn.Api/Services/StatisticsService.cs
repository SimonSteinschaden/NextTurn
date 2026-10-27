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
        var result = new PlayerStatistics
        {
            Favorites = count,
            Likes = favorites.Count(i => i.Action == SwipeAction.Like),
            SuperLikes = favorites.Count(i => i.Action == SwipeAction.SuperLike),
            AverageRank = Average(g => g.BggRank),
            AverageRating = Average(g => g.AverageRating),
            AveragePlayTime = Average(Duration),
            AverageComplexity = Average(g => g.ComplexityWeight > 0 ? g.ComplexityWeight : null),
            Categories = Distribution(g => g.Categories),
            Mechanics = Distribution(g => g.Mechanics),
            PlayerCounts = Distribution(g =>
                g.MinPlayers > 0 && g.MaxPlayers > 0
                    ? Enumerable
                        .Range(
                            Math.Max(1, g.MinPlayers.Value),
                            Math.Max(
                                0,
                                Math.Min(100, g.MaxPlayers.Value)
                                    - Math.Max(1, g.MinPlayers.Value)
                                    + 1
                            )
                        )
                        .Select(n => n.ToString())
                    : []
            ),
            RecommendedPlayerCounts = Distribution(g =>
                g.RecommendedPlayerCounts.Select(n => n.ToString())
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
                Duration(g) is { } t
                    ?
                    [
                        t <= 30 ? "Bis 30 Min."
                        : t <= 60 ? "31–60 Min."
                        : t <= 120 ? "61–120 Min."
                        : "Über 120 Min.",
                    ]
                    : []
            ),
            MissingData = new()
            {
                ["Ranking"] = games.Count(g => !g.BggRank.HasValue),
                ["Bewertung"] = games.Count(g => !g.AverageRating.HasValue),
                ["Spieldauer"] = games.Count(g => !Duration(g).HasValue),
                ["Komplexität"] = games.Count(g => !(g.ComplexityWeight > 0)),
                ["Kategorien"] = games.Count(g => g.Categories.Length == 0),
                ["Mechaniken"] = games.Count(g => g.Mechanics.Length == 0),
                ["Spieleranzahl"] = games.Count(g => !(g.MinPlayers > 0) || !(g.MaxPlayers > 0)),
                ["BGG-Spielerempfehlung"] = games.Count(g => g.RecommendedPlayerCounts.Length == 0),
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
                            + "/5"
                        : ""
                )
                + (
                    result.AveragePlayTime is { } time
                        ? ". Typische mittlere Spieldauer: " + Math.Round(time) + " Minuten."
                        : "."
                ),
        };
        return result;
    }
}
