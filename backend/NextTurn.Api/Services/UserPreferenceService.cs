using NextTurn.Api.Models;

namespace NextTurn.Api.Services
{
    public class UserPreferenceService
    {
        private readonly Dictionary<string, UserPreferenceProfile> _profiles
            = new();

        private readonly BggService _bggService;

        public UserPreferenceService(BggService bggService)
        {
            _bggService = bggService;
        }

        public UserPreferenceProfile GetOrCreateProfile(string sessionId)
        {
            if (_profiles.TryGetValue(sessionId, out var profile))
            {
                return profile;
            }

            profile = new UserPreferenceProfile
            {
                SessionId = sessionId
            };

            _profiles[sessionId] = profile;

            return profile;
        }

        public async Task<UserPreferenceProfile> ApplySwipeAsync(
            SwipeFeedbackDto feedback)
        {
            var profile = GetOrCreateProfile(feedback.SessionId);

            var game = await _bggService.GetBoardGameAsync(feedback.GameId);

            if (game == null)
            {
                return profile;
            }

            var weight = feedback.Action switch
            {
                SwipeAction.Like => 1.0,
                SwipeAction.SuperLike => 3.0,
                SwipeAction.Dislike => -1.0,
                SwipeAction.Skip => 0.0,
                _ => 0.0
            };

            foreach (var category in game.Categories)
            {
                AddScore(profile.CategoryScores, category, weight);
            }

            foreach (var mechanic in game.Mechanics)
            {
                AddScore(profile.MechanicScores, mechanic, weight);
            }

            if (feedback.Action != SwipeAction.Skip && game.Complexity.HasValue)
            {
                profile.PreferredComplexity =
                    CalculateNewAverage(
                        profile.PreferredComplexity,
                        game.Complexity.Value,
                        profile.TotalSwipes
                    );
            }

            if (feedback.Action != SwipeAction.Skip)
            {
                profile.TotalSwipes++;
            }

            return profile;
        }

        private void AddScore(
            Dictionary<string, double> scores,
            string key,
            double value)
        {
            if (!scores.ContainsKey(key))
            {
                scores[key] = 0;
            }

            scores[key] += value;
        }

        private double CalculateNewAverage(
            double currentAverage,
            double newValue,
            int currentCount)
        {
            if (currentCount == 0)
            {
                return newValue;
            }

            return ((currentAverage * currentCount) + newValue)
                   / (currentCount + 1);
        }
    }
}