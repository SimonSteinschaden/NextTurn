using Microsoft.AspNetCore.Mvc;

namespace NextTurn.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "NextTurn API is running"
            });        
        }    
    }
}