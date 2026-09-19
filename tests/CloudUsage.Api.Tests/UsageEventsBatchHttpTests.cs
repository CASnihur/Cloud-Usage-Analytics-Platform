using System.Net;
using System.Net.Http.Json;
using System.Text;
using CloudUsage.Api.Application.UsageEvents;
using CloudUsage.Api.Contracts.UsageEvents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudUsage.Api.Tests;

public sealed class UsageEventsBatchHttpTests
{
    private sealed class UnexpectedIngestionService : IUsageEventIngestionService
    {
        public int Calls { get; private set; }
        public Task<UsageEventIngestionResult> IngestAsync(
            IngestUsageEventCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("Invalid events must not reach ingestion.");
        }
    }

    private static WebApplicationFactory<Program> Factory(UnexpectedIngestionService service) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.UseSetting("ConnectionStrings:UsageAnalyticsDatabase", "Server=unused;Database=unused");
            builder.ConfigureServices(services => services.AddSingleton<IUsageEventIngestionService>(service));
        });

    [Fact]
    public async Task InvalidItems_ReturnIndependentErrorsWithoutIngestion()
    {
        var service = new UnexpectedIngestionService();
        using var factory = Factory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var content = new StringContent("""
            {"events":[
              123,
              {"eventId":"invalid-guid"},
              {"eventId":"73f529e4-1e11-4c7f-8ec5-d04d06e40f67","userId":"user-1","productCode":"studio","eventType":"feature_used"},
              {"eventId":"00000000-0000-0000-0000-000000000000","userId":"user-1","productCode":"studio","eventType":"feature_used","occurredAtUtc":"2026-09-05T12:00:00Z"}
            ]}
            """, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/usage-events/batch", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateUsageEventBatchResponse>();
        Assert.NotNull(body);
        Assert.Equal(4, body.Results.Count);
        var expectedFields = new[] { "$", "$.eventId", "OccurredAtUtc", "EventId" };
        for (var index = 0; index < body.Results.Count; index++)
        {
            var item = body.Results[index];
            Assert.Equal(index, item.Index);
            Assert.Equal(400, item.Status);
            Assert.NotNull(item.Errors);
            Assert.Single(item.Errors);
            Assert.True(item.Errors.ContainsKey(expectedFields[index]));
            Assert.Null(item.Event);
        }
        Assert.Equal(0, service.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"events\":null}")]
    [InlineData("{\"events\":[]}")]
    [InlineData("{\"events\":{}}")]
    [InlineData("{broken-json")]
    public async Task InvalidEnvelope_ReturnsOverall400(string json)
    {
        var service = new UnexpectedIngestionService();
        using var factory = Factory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/usage-events/batch", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, service.Calls);
    }
}
