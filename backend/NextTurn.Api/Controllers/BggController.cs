using Microsoft.AspNetCore.Mvc;
using NextTurn.Api.Services;

namespace NextTurn.Api.Controllers;

[ApiController, Route("api/bgg")]
public class BggController(BggService bgg) : ControllerBase
{
    // Legacy explicit lookup. Local catalog/search/recommendations never call this endpoint.
    [HttpGet("{id:int:min(1)}")]
    public async Task<IActionResult> GetBoardGame(int id, CancellationToken ct)
    {
        var game = await bgg.GetBoardGameAsync(id, ct);
        return game == null
            ? NotFound(new { message = "Spiel bei BGG nicht gefunden." })
            : Ok(game);
    }
}
