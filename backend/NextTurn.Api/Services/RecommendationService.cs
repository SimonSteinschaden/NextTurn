using NextTurn.Api.Entities;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;

namespace NextTurn.Api.Services;

public class RecommendationService(
    IGameRepository games,
    UserPreferenceService preferences,
    IConfiguration configuration
)
{
    public async Task<List<BoardGameDto>> GetRecommendationsAsync(
        string? sessionId,
        int? players,
        List<string> preferredCategories,
        List<string> preferredMechanics,
        CancellationToken ct = default
    )
    {
        var profile = string.IsNullOrWhiteSpace(sessionId)
            ? new UserPreferenceProfile()
            : await preferences.GetProfileAsync(sessionId, ct);
        var categories = profile
            .CategoryScores.Where(p => p.Value > 0)
            .OrderByDescending(p => p.Value)
            .Take(5)
            .Select(p => p.Key)
            .Concat(preferredCategories)
            .Distinct()
            .ToArray();
        var mechanics = profile
            .MechanicScores.Where(p => p.Value > 0)
            .OrderByDescending(p => p.Value)
            .Take(5)
            .Select(p => p.Key)
            .Concat(preferredMechanics)
            .Distinct()
            .ToArray();
        var limit = Math.Clamp(
            configuration.GetValue("Recommendations:CandidateLimit", 300),
            40,
            1000
        );
        var preferred = await games.CandidatesAsync(
            sessionId,
            players,
            categories,
            mechanics,
            false,
            limit,
            ct
        );
        var explore = await games.CandidatesAsync(sessionId, players, [], [], true, limit, ct);
        return SelectRecommendations(
            preferred,
            explore,
            profile,
            players,
            Math.Clamp(configuration.GetValue("Recommendations:PageSize", 20), 5, 50),
            Math.Clamp(configuration.GetValue("Recommendations:ExplorationRatio", 0.2), 0.1, 0.5),
            preferredCategories,
            preferredMechanics
        );
    }

    public static List<BoardGameDto> SelectRecommendations(
        IReadOnlyList<Game> preferred,
        IReadOnlyList<Game> exploration,
        UserPreferenceProfile profile,
        int? players,
        int count,
        double explorationRatio,
        IReadOnlyList<string>? categories = null,
        IReadOnlyList<string>? mechanics = null
    )
    {
        var rated = profile.RatedGameIds.ToHashSet();
        bool Eligible(Game g) =>
            !rated.Contains(g.BggId)
            && g.DetailsUpdatedAt != null
            && (
                !players.HasValue
                || (
                    g.MinPlayers > 0
                    && g.MaxPlayers > 0
                    && g.MinPlayers <= players
                    && g.MaxPlayers >= players
                )
            );
        double Score(Game g)
        {
            var categoryScore = g.Categories.Sum(c => profile.CategoryScores.GetValueOrDefault(c));
            var mechanicScore = g.Mechanics.Sum(m => profile.MechanicScores.GetValueOrDefault(m));
            var score = categoryScore * 25 + mechanicScore * 20 + (g.AverageRating ?? 0) * 2;
            if (categories != null)
                score += g.Categories.Count(categories.Contains) * 25;
            if (mechanics != null)
                score += g.Mechanics.Count(mechanics.Contains) * 20;
            if (profile.PreferredComplexity > 0 && g.ComplexityWeight is { } w)
                score += Math.Max(0, 20 - Math.Abs(w - profile.PreferredComplexity) * 10);
            if (profile.PreferredPlayTime is { } pt && StatisticsService.Duration(g) is { } t)
                score += Math.Max(0, 15 - Math.Abs(t - pt) / Math.Max(1, pt) * 15);
            if (players is { } p)
                score +=
                    g.BestPlayerCounts.Contains(p) ? 20
                    : g.RecommendedPlayerCounts.Contains(p) ? 10
                    : 0;
            if (profile.PreferredPlayerCount is { } pc && g.MinPlayers <= pc && g.MaxPlayers >= pc)
                score += 5;
            return score;
        }
        var all = preferred.Concat(exploration).Where(Eligible).DistinctBy(g => g.BggId).ToList();
        var positiveCategories = profile
            .CategoryScores.Where(p => p.Value > 0)
            .Select(p => p.Key)
            .ToHashSet();
        var positiveMechanics = profile
            .MechanicScores.Where(p => p.Value > 0)
            .Select(p => p.Key)
            .ToHashSet();
        // Reserve the discovery share before filling the personalized list, so high scores cannot consume it.
        var discovery = exploration
            .Where(Eligible)
            .DistinctBy(g => g.BggId)
            .OrderBy(g =>
                g.Categories.Count(positiveCategories.Contains)
                + g.Mechanics.Count(positiveMechanics.Contains)
            )
            .Take(Math.Max(1, (int)Math.Ceiling(Math.Min(count, all.Count) * explorationRatio)))
            .ToList();
        var discoveryIds = discovery.Select(g => g.BggId).ToHashSet();
        var personal = all.Where(g => !discoveryIds.Contains(g.BggId))
            .OrderByDescending(Score)
            .ThenBy(g => g.BggRank)
            .Take(Math.Max(0, count - discovery.Count))
            .ToList();
        var result = new List<BoardGameDto>();
        var pIndex = 0;
        var eIndex = 0;
        while (pIndex < personal.Count || eIndex < discovery.Count)
        {
            var takeExplore =
                eIndex < discovery.Count
                && (pIndex >= personal.Count || (result.Count + 1) % 5 == 0);
            var game = takeExplore ? discovery[eIndex++] : personal[pIndex++];
            var dto = GameMapping.ToDto(game);
            dto.RecommendationScore = Math.Round(Score(game), 2);
            dto.RecommendationReason =
                takeExplore ? "Neue Richtung entdecken"
                : profile.TotalSwipes > 0 ? "Passend zu deinem Spielerprofil"
                : "Für dich entdecken";
            result.Add(dto);
        }
        return result;
    }
}
