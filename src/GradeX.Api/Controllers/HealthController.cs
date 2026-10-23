
using Microsoft.AspNetCore.Mvc;

namespace GradeX.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            application = "GradeX API",
            version = "0.1.0"
        });
    }
}
