using CloudUsage.Api.Contracts.Health;
using Microsoft.AspNetCore.Mvc;

namespace CloudUsage.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        var response = new HealthResponse(
            Status: "Healthy",
            CheckedAtUtc: DateTimeOffset.UtcNow);

        return Ok(response);
    }
}
