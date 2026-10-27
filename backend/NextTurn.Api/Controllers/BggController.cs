using Microsoft.AspNetCore.Mvc;
using NextTurn.Api.Services;

namespace NextTurn.Api.Controllers
{
    [ApiController]
    [Route("api/bgg")]
    public class BggController : ControllerBase
    {
        private readonly BggService _bggService;

        public BggController(BggService bggService)
        {
            _bggService = bggService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBoardGame(int id)
        {            
            try
            {
                var game = await _bggService.GetBoardGameAsync(id);
                if (game == null)
                {
                    return NotFound(new { message = $"Board game with ID {id} not found." });
                }
                return Ok(game);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching board game data.", details = ex.Message });
            }
        }
    }
}