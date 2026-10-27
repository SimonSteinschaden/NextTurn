using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;

namespace NextTurn.Api.Services;

public class UserPreferenceService(IInteractionRepository interactions, IGameRepository games)
{
    public static bool ValidSession(string? id) =>
        id != null
        && id.Length is > 0 and <= 100
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public async Task<UserPreferenceProfile> GetProfileAsync(
        string sessionId,
        CancellationToken ct = default
    ) => BuildProfile(sessionId, await interactions.CurrentAsync(sessionId, ct));

    public async Task<UserPreferenceProfile> ApplySwipeAsync(
        SwipeFeedbackDto feedback,
        CancellationToken ct = default
    )
    {
        if (
            !ValidSession(feedback.SessionId)
            || !Enum.IsDefined(feedback.Action)
            || feedback.GameId < 1
        )
            throw new ArgumentException("Ungültige Session oder Bewertung.");
        if (await games.FindAsync(feedback.GameId, ct) == null)
            throw new KeyNotFoundException("Spiel nicht im lokalen Katalog.");
        await interactions.SaveAsync(feedback.SessionId, feedback.GameId, feedback.Action, ct);
        return await GetProfileAsync(feedback.SessionId, ct);
    }

    public static UserPreferenceProfile BuildProfile(
        string sessionId,
        IReadOnlyList<GameInteraction> current
    )
    {
        var profile = new UserPreferenceProfile
        {
            SessionId = sessionId,
            DesignerScores = DesignerPreference.Learn(current),
            TotalSwipes = current.Count(i => i.Action.HasValue && i.Action != SwipeAction.Skip),
            RatedGameIds = current
                .Where(i => i.Action.HasValue)
                .Select(i => i.BggId)
                .Distinct()
                .ToArray(),
        };
        foreach (var interaction in current.Where(i => i.Action.HasValue))
        {
            var weight = interaction.Action switch
            {
                SwipeAction.Like => 1d,
                SwipeAction.SuperLike => 3d,
                SwipeAction.Dislike => -1d,
                _ => 0d,
            };
            foreach (var category in interaction.Game.Categories.Distinct())
                profile.CategoryScores[category] =
                    profile.CategoryScores.GetValueOrDefault(category) + weight;
            foreach (var mechanic in interaction.Game.Mechanics.Distinct())
                profile.MechanicScores[mechanic] =
                    profile.MechanicScores.GetValueOrDefault(mechanic) + weight;
        }
        var positive = current
            .Where(i => i.Action is SwipeAction.Like or SwipeAction.SuperLike)
            .ToList();
        double? Weighted(Func<Game, double?> selector)
        {
            var known = positive.Where(i => selector(i.Game).HasValue).ToList();
            return known.Count == 0
                ? null
                : known.Sum(i =>
                    selector(i.Game)!.Value * (i.Action == SwipeAction.SuperLike ? 3 : 1)
                ) / known.Sum(i => i.Action == SwipeAction.SuperLike ? 3d : 1d);
        }
        profile.PreferredComplexity =
            Weighted(g => g.ComplexityWeight > 0 ? g.ComplexityWeight : null) ?? 0;
        profile.PreferredPlayTime = Weighted(g => StatisticsService.Duration(g));
        profile.PreferredPlayerCount = Weighted(g =>
            g.BestPlayerCounts.Length > 0 ? g.BestPlayerCounts.Average()
            : g.MinPlayers > 0 && g.MaxPlayers > 0 ? (g.MinPlayers.Value + g.MaxPlayers.Value) / 2d
            : null
        );
        return profile;
    }
}
