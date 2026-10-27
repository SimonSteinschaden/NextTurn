using Microsoft.AspNetCore.Mvc;
using NextTurn.Api.Models;
using NextTurn.Api.Services;

namespace NextTurn.Api.Controllers
{
    [ApiController]
    [Route("api/swipes")]
    public class SwipesController : ControllerBase
    {
        private readonly UserPreferenceService _userPreferenceService;

        public SwipesController(
            UserPreferenceService userPreferenceService)
        {
            _userPreferenceService = userPreferenceService;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSwipe(
            [FromBody] SwipeFeedbackDto feedback)
        {
            if (string.IsNullOrWhiteSpace(feedback.SessionId))
            {
                return BadRequest(new
                {
                    message = "SessionId is required."
                });
            }

            var profile =
                await _userPreferenceService.ApplySwipeAsync(
                    feedback
                );

            return Ok(profile);
        }
    }
}