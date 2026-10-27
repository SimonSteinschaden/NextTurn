using Microsoft.AspNetCore.Mvc;
using NextTurn.Api.Models;
using NextTurn.Api.Services;

namespace NextTurn.Api.Controllers;

[ApiController, Route("api/swipes")]
public class SwipesController(UserPreferenceService preferences) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit(
        [FromBody] SwipeFeedbackDto feedback,
        CancellationToken ct
    )
    {
        try
        {
            return Ok(await preferences.ApplySwipeAsync(feedback, ct));
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Ungültige Session, Spiel-ID oder Bewertung." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Spiel nicht im lokalen Katalog." });
        }
    }
}
