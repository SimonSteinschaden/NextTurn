using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NextTurn.Api.Models;
using NextTurn.Api.Repositories;
using NextTurn.Api.Services;

namespace NextTurn.Api.Controllers;

[ApiController, Route("api/games")]
public class GamesController(
    IGameRepository games,
    RecommendationService recommendations,
    MatchQualityService matches,
    ImportState importState
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] CatalogQuery query,
        string? sessionId,
        CancellationToken ct
    )
    {
        if (sessionId != null && !UserPreferenceService.ValidSession(sessionId))
            return BadRequest(new { message = "Ungültige Session." });
        var result = await games.SearchAsync(query, sessionId, false, ct);
        await matches.ApplyAsync(result.Items, sessionId, ct);
        return Ok(result);
    }

    [HttpGet("ranked")]
    public async Task<IActionResult> Ranked(CancellationToken ct) =>
        Ok((await games.SearchAsync(new() { PageSize = 20 }, null, false, ct)).Items);

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) =>
        Ok(
            new
            {
                catalog = await games.StatusAsync(importState.Running, ct),
                lastError = importState.LastError,
            }
        );

    [HttpGet("facets")]
    public async Task<IActionResult> Facets(CancellationToken ct) =>
        Ok(await games.FacetsAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, string? sessionId, CancellationToken ct)
    {
        if (sessionId != null && !UserPreferenceService.ValidSession(sessionId))
            return BadRequest(new { message = "Ungültige Session." });
        var game = await games.FindAsync(id, ct);
        if (game == null)
            return NotFound(new { message = "Spiel nicht im lokalen Katalog." });
        var context = await matches.LoadAsync(sessionId, ct);
        var dto = GameMapping.ToDto(
            game,
            context.Actions.TryGetValue(id, out var action) ? action : null
        );
        MatchQualityService.Apply([dto], context);
        return Ok(dto);
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> Recommendations(
        [Required] string sessionId,
        [Range(1, 100)] int? players,
        string? categories,
        string? mechanics,
        [Range(1, 10000)] int? maxRank,
        CancellationToken ct
    )
    {
        if (!UserPreferenceService.ValidSession(sessionId))
            return BadRequest(new { message = "Ungültige Session." });
        return Ok(
            await recommendations.GetRecommendationsAsync(
                sessionId,
                players,
                Split(categories),
                Split(mechanics),
                ct,
                maxRank
            )
        );
    }

    private static List<string> Split(string? text) =>
        text?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(10)
            .ToList()
        ?? [];
}
