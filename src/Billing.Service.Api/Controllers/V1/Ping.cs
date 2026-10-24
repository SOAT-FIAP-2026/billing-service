using Microsoft.AspNetCore.Mvc;

namespace Billing.Service.Api.Controllers
{
    [ApiController]
    [Route("api/ping")]
    public class PingController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
             return StatusCode(200, new
            {
                message = "pong"
            });
        }
    }
}
