using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;

namespace NextTurn.Api.Services;

public class MatchQualityService(IInteractionRepository interactions)
{
    internal sealed record Features(
        HashSet<string> Categories,
        HashSet<string> Mechanics,
        double? Complexity,
        double? Duration,
        (int Min, int Max)? Players
    );

    internal sealed record Reference(int BggId, double Weight, Features Features);

    public sealed class Context
    {
        internal List<Reference> References { get; } = [];
        public Dictionary<int, SwipeAction> Actions { get; } = [];
        public HashSet<int> OwnedGameIds { get; } = [];
    }

    public async Task<Context> LoadAsync(string? sessionId, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(sessionId)
            ? BuildContext([])
            : BuildContext(await interactions.CurrentAsync(sessionId, ct));

    public async Task ApplyAsync(
        IReadOnlyList<BoardGameDto> games,
        string? sessionId,
        CancellationToken ct
    )
    {
        if (games.Count == 0)
            return;
        Apply(games, await LoadAsync(sessionId, ct));
    }

    public static Context BuildContext(IEnumerable<GameInteraction> interactions)
    {
        var context = new Context();
        // Current state per game, including changed/removed favorites. History is never counted twice.
        foreach (
            var current in interactions
                .GroupBy(i => i.BggId)
                .Select(group => group.MaxBy(i => i.UpdatedAt)!)
        )
        {
            if (current.Action.HasValue)
                context.Actions[current.BggId] = current.Action.Value;
            if (current.IsOwned)
                context.OwnedGameIds.Add(current.BggId);
            if (current.Action is not (SwipeAction.Like or SwipeAction.SuperLike))
                continue;
            var g = current.Game;
            context.References.Add(
                new(
                    current.BggId,
                    current.Action == SwipeAction.SuperLike ? 3 : 1,
                    Snapshot(
                        g.Categories,
                        g.Mechanics,
                        g.ComplexityWeight,
                        g.MinPlayTime,
                        g.MaxPlayTime,
                        g.PlayingTime,
                        g.MinPlayers,
                        g.MaxPlayers
                    )
                )
            );
        }
        return context;
    }

    public static void Apply(IReadOnlyList<BoardGameDto> games, Context context)
    {
        foreach (var game in games)
        {
            game.MatchQuality = Calculate(game, context);
            game.IsOwned = context.OwnedGameIds.Contains(game.Id);
        }
    }

    public static GameMatchQuality Calculate(BoardGameDto game, Context context)
    {
        var candidate = Snapshot(
            game.Categories,
            game.Mechanics,
            game.Complexity,
            game.MinPlayTime,
            game.MaxPlayTime,
            game.PlayingTime,
            game.MinPlayers,
            game.MaxPlayers
        );
        var weightedSimilarity = 0d;
        var weightedCoverage = 0d;
        var totalWeight = 0d;
        var compared = 0;
        var available = 0;
        foreach (var reference in context.References)
        {
            // A favorite cannot manufacture a perfect match by comparing against itself.
            if (reference.BggId == game.Id)
                continue;
            available++;
            var favorite = reference.Features;
            var similarity = 0d;
            var coverage = 0d;
            void Add(double? value, double importance)
            {
                if (!value.HasValue)
                    return;
                similarity += Math.Clamp(value.Value, 0, 1) * importance;
                coverage += importance;
            }
            Add(Cosine(candidate.Categories, favorite.Categories), .35);
            Add(Cosine(candidate.Mechanics, favorite.Mechanics), .35);
            Add(
                candidate.Complexity is { } a && favorite.Complexity is { } b
                    ? 1 - Math.Abs(a - b) / 4
                    : null,
                .15
            );
            Add(
                candidate.Duration is { } x && favorite.Duration is { } y
                    ? Math.Min(x, y) / Math.Max(x, y)
                    : null,
                .10
            );
            Add(PlayerSimilarity(candidate.Players, favorite.Players), .05);
            // One incidental matching value (e.g. duration alone) is not enough for a meaningful percentage.
            if (coverage + 1e-9 < .30)
                continue;
            weightedSimilarity += similarity / coverage * reference.Weight;
            weightedCoverage += coverage * reference.Weight;
            totalWeight += reference.Weight;
            compared++;
        }
        if (compared == 0)
            return new(null, 0, available, 0, true);
        var averageCoverage = weightedCoverage / totalWeight;
        return new(
            Math.Round(
                Math.Clamp(100 * weightedSimilarity / totalWeight, 0, 100),
                0,
                MidpointRounding.AwayFromZero
            ),
            compared,
            available,
            Math.Round(100 * averageCoverage, 0),
            compared < 5 || averageCoverage + 1e-9 < .70 || compared < available
        );
    }

    private static Features Snapshot(
        IEnumerable<string>? categories,
        IEnumerable<string>? mechanics,
        double? complexity,
        int? minTime,
        int? maxTime,
        int? time,
        int? minPlayers,
        int? maxPlayers
    )
    {
        static HashSet<string> Tags(IEnumerable<string>? tags) =>
            (tags ?? [])
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        double? duration =
            minTime > 0 && maxTime >= minTime ? (minTime.Value + (double)maxTime.Value) / 2
            : time > 0 ? time
            : null;
        return new(
            Tags(categories),
            Tags(mechanics),
            complexity is >= 1 and <= 5 ? complexity : null,
            duration,
            minPlayers > 0 && maxPlayers >= minPlayers ? (minPlayers.Value, maxPlayers.Value) : null
        );
    }

    private static double? Cosine(HashSet<string> a, HashSet<string> b) =>
        a.Count == 0 || b.Count == 0
            ? null
            : a.Count(b.Contains) / Math.Sqrt((double)a.Count * b.Count);

    private static double? PlayerSimilarity((int Min, int Max)? a, (int Min, int Max)? b)
    {
        if (a is not { } first || b is not { } second)
            return null;
        var intersection = Math.Max(
            0L,
            (long)Math.Min(first.Max, second.Max) - Math.Max(first.Min, second.Min) + 1
        );
        var firstSize = (long)first.Max - first.Min + 1;
        var secondSize = (long)second.Max - second.Min + 1;
        return intersection / (double)(firstSize + secondSize - intersection);
    }
}
