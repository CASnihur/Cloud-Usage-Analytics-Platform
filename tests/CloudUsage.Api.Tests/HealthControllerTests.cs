using CloudUsage.Api.Contracts;
using CloudUsage.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace CloudUsage.Api.Tests;

public sealed class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsHealthyResponse()
    {
        var controller = new HealthController();

        var result = controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HealthResponse>(okResult.Value);

        Assert.Equal("Healthy", response.Status);
        Assert.True(response.CheckedAtUtc <= DateTimeOffset.UtcNow);
    }
}
