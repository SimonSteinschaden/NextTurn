using Microsoft.AspNetCore.Mvc;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;
using NextTurn.Api.Services;

namespace NextTurn.Api.Controllers;

[ApiController, Route("api/player")]
public class PlayerController(
    IGameRepository games,
    StatisticsService statistics,
    UserPreferenceService preferences,
    MatchQualityService matches
) : ControllerBase
{
    [HttpGet("favorites")]
    public async Task<IActionResult> Favorites(
        string sessionId,
        [FromQuery] CatalogQuery query,
        CancellationToken ct
    )
    {
        if (!UserPreferenceService.ValidSession(sessionId))
            return BadRequest(new { message = "Ungültige Session." });
        var result = await games.SearchAsync(query, sessionId, true, ct);
        await matches.ApplyAsync(result.Items, sessionId, ct);
        return Ok(result);
    }

    [HttpDelete("favorites/{id:int}")]
    public async Task<IActionResult> RemoveFavorite(int id, string sessionId, CancellationToken ct)
    {
        if (!UserPreferenceService.ValidSession(sessionId))
            return BadRequest(new { message = "Ungültige Session." });
        try
        {
            await preferences.ApplySwipeAsync(
                new()
                {
                    SessionId = sessionId,
                    GameId = id,
                    Action = SwipeAction.Skip,
                },
                ct
            );
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> Statistics(string sessionId, CancellationToken ct)
    {
        if (!UserPreferenceService.ValidSession(sessionId))
            return BadRequest(new { message = "Ungültige Session." });
        return Ok(await statistics.GetAsync(sessionId, ct));
    }
}
