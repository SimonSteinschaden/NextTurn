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
        var cutoff = Math.Clamp(
            configuration.GetValue("Recommendations:TopRankCutoff", 500),
            1,
            10000
        );
        // Each bounded pool contains both rank bands; neither the tail nor less familiar genres are truncated away.
        var preferred = await games.CandidatesAsync(
            sessionId,
            players,
            categories,
            mechanics,
            false,
            limit,
            ct,
            cutoff
        );
        var explore = await games.CandidatesAsync(
            sessionId,
            players,
            categories,
            mechanics,
            true,
            limit,
            ct,
            cutoff
        );
        return SelectRecommendations(
            preferred,
            explore,
            profile,
            players,
            Math.Clamp(configuration.GetValue("Recommendations:PageSize", 20), 5, 50),
            Math.Clamp(configuration.GetValue("Recommendations:ExplorationRatio", 0.2), 0.1, 0.5),
            preferredCategories,
            preferredMechanics,
            cutoff,
            Math.Clamp(configuration.GetValue("Recommendations:TopRankProbability", 0.8), 0, 1),
            Math.Clamp(configuration.GetValue("Recommendations:RankDecayExponent", 0.65), 0.1, 2)
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
        IReadOnlyList<string>? mechanics = null,
        int topRankCutoff = 500,
        double topRankProbability = 0.8,
        double rankDecayExponent = 0.65,
        Random? random = null
    )
    {
        if (count <= 0)
            return [];
        random ??= Random.Shared;
        topRankCutoff = Math.Clamp(topRankCutoff, 1, 10000);
        topRankProbability = Math.Clamp(topRankProbability, 0, 1);
        rankDecayExponent = Math.Clamp(rankDecayExponent, 0.1, 2);
        var rated = profile.RatedGameIds.ToHashSet();
        bool Eligible(Game g) =>
            !rated.Contains(g.BggId)
            && g.BggRank is > 0 and <= 10000
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
        var remaining = preferred
            .Concat(exploration)
            .Where(Eligible)
            .DistinctBy(g => g.BggId)
            .ToList();
        var personalIds = preferred.Select(g => g.BggId).ToHashSet();
        var explorationIds = exploration.Select(g => g.BggId).ToHashSet();
        var scores = remaining.ToDictionary(g => g.BggId, Score);
        // Positive, bounded profile factors preserve learning without allowing any score to override the rank-band draw.
        // Rank decay applies inside favorite genres/mechanics as well: equally fitting games with better ranks get larger weights.
        var weights = remaining.ToDictionary(
            g => g.BggId,
            g =>
                Math.Exp(Math.Clamp(scores[g.BggId] / 100d, -4, 4))
                / Math.Pow(g.BggRank!.Value, rankDecayExponent)
        );
        var positiveCategories = profile
            .CategoryScores.Where(p => p.Value > 0)
            .Select(p => p.Key)
            .Concat(categories ?? [])
            .ToHashSet();
        var positiveMechanics = profile
            .MechanicScores.Where(p => p.Value > 0)
            .Select(p => p.Key)
            .Concat(mechanics ?? [])
            .ToHashSet();
        int Familiarity(Game g) =>
            g.Categories.Count(positiveCategories.Contains)
            + g.Mechanics.Count(positiveMechanics.Contains);
        var size = Math.Min(count, remaining.Count);
        var discoveryCount = (int)Math.Ceiling(size * Math.Clamp(explorationRatio, 0, 1));
        // Randomize discovery positions: the client reranks after every swipe, so a fixed fifth slot would never become the next card.
        var slots = Enumerable.Range(0, size).ToArray();
        for (var i = slots.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }
        var discoverySlots = slots.Take(discoveryCount).ToHashSet();
        var result = new List<BoardGameDto>(size);
        for (var slot = 0; slot < size; slot++)
        {
            // Independent axes: 80% Top 500 / 20% tail, and (by default) 80% personalized / 20% discovery.
            var topBand = random.NextDouble() < topRankProbability;
            var band = remaining.Where(g => (g.BggRank <= topRankCutoff) == topBand).ToList();
            if (band.Count == 0)
                band = remaining.ToList(); // Exhaustion or player filters must not create repeats or empty slots.
            var discovery = discoverySlots.Contains(slot);
            var sourceIds = discovery ? explorationIds : personalIds;
            var candidates = band.Where(g => sourceIds.Contains(g.BggId)).ToList();
            if (candidates.Count == 0)
                candidates = band;
            if (discovery)
            {
                var minimumFamiliarity = candidates.Min(Familiarity);
                candidates = candidates.Where(g => Familiarity(g) == minimumFamiliarity).ToList();
            }
            var draw = random.NextDouble() * candidates.Sum(g => weights[g.BggId]);
            var selected = candidates[^1];
            foreach (var candidate in candidates)
            {
                draw -= weights[candidate.BggId];
                if (draw < 0)
                {
                    selected = candidate;
                    break;
                }
            }
            remaining.Remove(selected);
            var dto = GameMapping.ToDto(selected);
            dto.RecommendationScore = Math.Round(scores[selected.BggId], 2);
            dto.RecommendationReason =
                discovery ? "Neue Richtung entdecken"
                : profile.TotalSwipes > 0 ? "Ranking und dein Spielerprofil"
                : "Nach BGG-Ranking gewichtet";
            result.Add(dto);
        }
        return result;
    }
}
