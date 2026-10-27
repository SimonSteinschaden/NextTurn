using NextTurn.Api.Models;
using NextTurn.Api.Repositories;

namespace NextTurn.Api.Services
{
    public class RecommendationService
    {
        private readonly CsvGameRepository _csvGameRepository;
        private readonly BggService _bggService;
        private readonly UserPreferenceService _userPreferenceService;

        public RecommendationService(
            CsvGameRepository csvGameRepository,
            BggService bggService,
            UserPreferenceService userPreferenceService)
        {
            _csvGameRepository = csvGameRepository;
            _bggService = bggService;
            _userPreferenceService = userPreferenceService;
        }

        public async Task<List<BoardGameDto>> GetRecommendationsAsync(
                                                                        string? sessionId,
                                                                        int? players,
                                                                        List<string> preferredCategories,
                                                                        List<string> preferredMechanics)
        {
            UserPreferenceProfile? profile = null;

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                profile = _userPreferenceService.GetOrCreateProfile(sessionId);
            }

            var rankedGames =
                _csvGameRepository
                    .LoadRankedGames()
                    .Take(20)
                    .ToList();

            var recommendations =
                new List<BoardGameDto>();

            foreach (var rankedGame in rankedGames)
            {
                try
                {
                    var game =
                        await _bggService.GetBoardGameAsync(
                            rankedGame.BggId
                        );

                    if (game == null)
                    {
                        continue;
                    }

                    game.BggRank =
                        rankedGame.BggRank;

                    if (players.HasValue)
                    {
                        var isTechnicallyPlayable =
                            game.MinPlayers.HasValue &&
                            game.MaxPlayers.HasValue &&
                            players.Value >= game.MinPlayers.Value &&
                            players.Value <= game.MaxPlayers.Value;

                        if (!isTechnicallyPlayable)
                        {
                            continue;
                        }

                    game.RecommendationScore =
                        CalculatePlayerCountScore(game, players.Value)
                        + CalculateCategoryMechanicScore(
                            game,
                            preferredCategories,
                            preferredMechanics)
                        + CalculatePreferenceScore(game, profile);

                    }
                    else
                    {
                    game.RecommendationScore =
                        CalculateCategoryMechanicScore(
                            game,
                            preferredCategories,
                            preferredMechanics)
                        + CalculatePreferenceScore(game, profile);
                    }

                    recommendations.Add(game);
                }
                catch
                {
                    // später logging
                }
            }

            return recommendations
                .OrderByDescending(x => x.RecommendationScore)
                .ThenBy(x => x.BggRank)
                .ToList();
        }

        private double CalculatePlayerCountScore(
            BoardGameDto game,
            int players)
        {
            var poll = game.PlayerCountPoll
                .FirstOrDefault(
                    x => x.PlayerCount == players.ToString()
                );

            if (poll == null)
            {
                return 0;
            }

            var totalVotes =
                poll.Best
                + poll.Recommended
                + poll.NotRecommended;

            if (totalVotes == 0)
            {
                return 0;
            }

            var bestRatio =
                (double)poll.Best / totalVotes;

            var recommendedRatio =
                (double)poll.Recommended / totalVotes;

            var notRecommendedRatio =
                (double)poll.NotRecommended / totalVotes;

            return
                bestRatio * 100
                + recommendedRatio * 60
                - notRecommendedRatio * 80;
        }

        private double CalculateCategoryMechanicScore(
            BoardGameDto game,
            List<string> preferredCategories,
            List<string> preferredMechanics)
        {
            var score = 0.0;

            foreach (var category in preferredCategories)
            {
                if (game.Categories.Any(x =>
                    string.Equals(
                        x,
                        category,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    score += 25;
                }
            }

            foreach (var mechanic in preferredMechanics)
            {
                if (game.Mechanics.Any(x =>
                    string.Equals(
                        x,
                        mechanic,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    score += 30;
                }
            }

            return score;
        }

        private double CalculatePreferenceScore(
        BoardGameDto game,
        UserPreferenceProfile? profile)
        {
            if (profile == null)
            {
                return 0;
            }

            var score = 0.0;

            foreach (var category in game.Categories)
            {
                if (profile.CategoryScores.TryGetValue(category, out var value))
                {
                    score += value * 15;
                }
            }

            foreach (var mechanic in game.Mechanics)
            {
                if (profile.MechanicScores.TryGetValue(mechanic, out var value))
                {
                    score += value * 20;
                }
            }

            if (profile.TotalSwipes > 0 && game.Complexity.HasValue)
            {
                var complexityDifference =
                    Math.Abs(game.Complexity.Value - profile.PreferredComplexity);

                score += Math.Max(0, 20 - complexityDifference * 10);
            }

            return score;
        }
    }
}