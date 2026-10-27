using Microsoft.AspNetCore.Mvc;
using NextTurn.Api.Repositories;
using NextTurn.Api.Services;

namespace NextTurn.Api.Controllers
{
    [ApiController]
    [Route("api/games")]
    public class GamesController : ControllerBase
    {
        private readonly CsvGameRepository _csvGameRepository;
        private readonly RecommendationService _recommendationService;

        public GamesController(
            CsvGameRepository csvGameRepository,
            RecommendationService recommendationService)
        {
            _csvGameRepository = csvGameRepository;
            _recommendationService = recommendationService;
        }

        [HttpGet("ranked")]
        public IActionResult GetRankedGames()
        {
            var games = _csvGameRepository
                .LoadRankedGames()
                .Take(20)
                .ToList();

            return Ok(games);
        }

        [HttpGet("recommendations")]
        public async Task<IActionResult> GetRecommendations(
            string? sessionId,
            int? players,
            string? categories,
            string? mechanics)
        {
            var preferredCategories =
                ParseCsvParameter(categories);

            var preferredMechanics =
                ParseCsvParameter(mechanics);

            var recommendations =
                await _recommendationService.GetRecommendationsAsync(
                    sessionId,
                    players,
                    preferredCategories,
                    preferredMechanics
                );

            return Ok(recommendations);
        }

        private List<string> ParseCsvParameter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }
    }
}