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
    MatchQualityService matches,
    OwnershipService ownership
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

    [HttpGet("owned")]
    public async Task<IActionResult> Owned(
        string sessionId,
        [FromQuery] CatalogQuery query,
        CancellationToken ct
    )
    {
        if (!UserPreferenceService.ValidSession(sessionId))
            return BadRequest(new { message = "Ungültige Session." });
        query.OwnedOnly = true;
        var result = await games.SearchAsync(query, sessionId, false, ct);
        await matches.ApplyAsync(result.Items, sessionId, ct);
        return Ok(result);
    }

    [HttpPut("owned/{id:int}")]
    public async Task<IActionResult> SetOwned(
        int id,
        string sessionId,
        [FromBody] OwnershipRequest request,
        CancellationToken ct
    )
    {
        if (!request.IsOwned.HasValue)
            return BadRequest(new { message = "Besitzstatus fehlt." });
        try
        {
            await ownership.SetAsync(sessionId, id, request.IsOwned.Value, ct);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Ungültige Session." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Spiel nicht im Katalog." });
        }
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
