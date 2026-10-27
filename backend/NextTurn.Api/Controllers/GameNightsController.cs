using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NextTurn.Api.Models;
using NextTurn.Api.Services;

namespace NextTurn.Api.Controllers;

[ApiController, Route("api/game-nights")]
public class GameNightsController(GameNightService service) : ControllerBase
{
    private async Task<IActionResult> Run(Func<Task<object>> work)
    {
        try
        {
            return Ok(await work());
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Spieleabend nicht gefunden." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public Task<IActionResult> List(
        string sessionId,
        [Range(1, 10000)] int page = 1,
        CancellationToken ct = default
    ) => Run(async () => await service.ListAsync(sessionId, page, ct));

    [HttpPost]
    public Task<IActionResult> Create(
        string sessionId,
        [FromBody] GameNightRequest request,
        CancellationToken ct
    ) => Run(async () => await service.CreateAsync(sessionId, request, ct));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Board(
        Guid id,
        string sessionId,
        [Range(1, 10000)] int page = 1,
        CancellationToken ct = default
    ) => Run(async () => await service.BoardAsync(id, sessionId, page, ct));

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(
        Guid id,
        string sessionId,
        [FromBody] GameNightRequest request,
        CancellationToken ct
    ) => Run(async () => await service.UpdateAsync(id, sessionId, request, ct));

    [HttpPost("{id:guid}/choices/{gameId:int}")]
    public Task<IActionResult> Choose(
        Guid id,
        int gameId,
        string sessionId,
        [FromBody] GameNightChoiceRequest request,
        CancellationToken ct
    ) =>
        Run(async () =>
            await service.ChooseAsync(id, sessionId, gameId, request.Action!.Value, ct)
        );
}
