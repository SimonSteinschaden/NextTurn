using NextTurn.Api.Entities;
using NextTurn.Api.Models;

namespace NextTurn.Api.Services;

public static class DesignerPreference
{
    public static IEnumerable<string> Names(IEnumerable<string>? designers) =>
        (designers ?? [])
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Where(n =>
                n.Length > 0
                && !n.Equals("(Uncredited)", StringComparison.OrdinalIgnoreCase)
                && !n.Equals("Uncredited", StringComparison.OrdinalIgnoreCase)
                && !n.Equals("(Unknown)", StringComparison.OrdinalIgnoreCase)
                && !n.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            )
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, double> Learn(IEnumerable<GameInteraction> interactions)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (
            var current in interactions
                .GroupBy(i => i.BggId)
                .Select(group => group.MaxBy(i => i.UpdatedAt)!)
        )
        {
            var weight = current.Action switch
            {
                SwipeAction.Like => 1d,
                SwipeAction.SuperLike => 3d,
                SwipeAction.Dislike => -1d,
                _ => 0d,
            };
            if (weight == 0)
                continue;
            foreach (var name in Names(current.Game.Designers))
                scores[name] = scores.GetValueOrDefault(name) + weight;
        }
        return scores;
    }

    public static double Score(
        IEnumerable<string>? designers,
        IReadOnlyDictionary<string, double> scores
    )
    {
        var known = Names(designers).Where(scores.ContainsKey).Select(d => scores[d]).ToArray();
        // Co-authors do not multiply the bonus; unknown co-authors do not dilute known preferences.
        return known.Length == 0 ? 0 : Math.Clamp(known.Average() * 10, -20, 20);
    }
}
