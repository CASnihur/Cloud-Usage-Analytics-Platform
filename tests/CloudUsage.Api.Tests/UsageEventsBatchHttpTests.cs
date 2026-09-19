using System.Net;
using System.Net.Http.Json;
using System.Text;
using CloudUsage.Api.Application.UsageEvents;
using CloudUsage.Api.Contracts.UsageEvents;
using CloudUsage.Api.Data.Entities;
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

    private sealed class RecordingService : IUsageEventIngestionService
    {
        public List<IngestUsageEventCommand> Commands { get; } = [];
        public int? FailOnCall { get; init; }
        public DateTimeOffset ReceiptTime { get; } = DateTimeOffset.Parse("2026-09-05T12:00:00Z");

        public Task<UsageEventIngestionResult> IngestAsync(
            IngestUsageEventCommand command, CancellationToken cancellationToken)
        {
            var duplicate = Commands.Any(previous => previous.EventId == command.EventId);
            Commands.Add(command);
            if (Commands.Count == FailOnCall)
                throw new InvalidOperationException("Private database failure details");
            return Task.FromResult<UsageEventIngestionResult>(duplicate
                ? new UsageEventIngestionResult.Duplicate(command.EventId)
                : new UsageEventIngestionResult.Created(42, command.EventId,
                    RawEventIngestionStatus.Pending, ReceiptTime));
        }
    }

    private static WebApplicationFactory<Program> Factory(IUsageEventIngestionService service) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            builder.UseSetting("ConnectionStrings:UsageAnalyticsDatabase", "Server=unused;Database=unused");
            builder.ConfigureServices(services => services.AddSingleton<IUsageEventIngestionService>(service));
        });

    private static object ValidEvent(Guid id) => new
    {
        eventId = id, userId = "user-1", productCode = "studio", eventType = "feature_used",
        occurredAtUtc = "2026-09-05T10:00:00Z", properties = new { featureName = "Analyzer" }
    };

    [Fact]
    public async Task MixedBatch_ReturnsOrderedOutcomesAndSkipsInvalidItem()
    {
        var service = new RecordingService();
        using var factory = Factory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var id = Guid.NewGuid();
        using var response = await client.PostAsJsonAsync("/api/usage-events/batch",
            new { events = new object[] { 123, ValidEvent(id), ValidEvent(id) } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateUsageEventBatchResponse>();
        Assert.NotNull(body);
        Assert.Equal(new[] { 0, 1, 2 }, body.Results.Select(item => item.Index));
        Assert.Equal(new[] { 400, 201, 409 }, body.Results.Select(item => item.Status));
        Assert.NotNull(body.Results[0].Errors);
        var created = body.Results[1].Event;
        Assert.NotNull(created);
        Assert.Equal(id, created.EventId);
        Assert.Equal(42, created.RawEventId);
        Assert.Equal("Pending", created.IngestionStatus);
        Assert.Equal(service.ReceiptTime, created.ReceivedAtUtc);
        Assert.Null(body.Results[1].Errors);
        Assert.Null(body.Results[2].Event);
        Assert.NotNull(body.Results[2].Detail);
        Assert.Equal(2, service.Commands.Count);
        Assert.All(service.Commands, command =>
        {
            Assert.Equal(id, command.EventId);
            Assert.Equal("user-1", command.UserExternalId);
            Assert.Equal("studio", command.ProductCode);
            Assert.Equal("feature_used", command.EventType);
            Assert.Equal(DateTimeOffset.Parse("2026-09-05T10:00:00Z"), command.OccurredAtUtc);
            using var properties = System.Text.Json.JsonDocument.Parse(command.PropertiesJson!);
            Assert.Equal("Analyzer", properties.RootElement.GetProperty("featureName").GetString());
        });
    }

    [Fact]
    public async Task UnexpectedFailure_StopsBeforeRemainingItems()
    {
        var service = new RecordingService { FailOnCall = 2 };
        using var factory = Factory(service);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/usage-events/batch",
            new { events = new[] { ValidEvent(Guid.NewGuid()), ValidEvent(Guid.NewGuid()), ValidEvent(Guid.NewGuid()) } });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(2, service.Commands.Count);
        Assert.DoesNotContain("Private database", await response.Content.ReadAsStringAsync());
    }

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
