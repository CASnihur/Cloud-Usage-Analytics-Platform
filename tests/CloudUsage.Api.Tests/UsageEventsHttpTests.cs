using System.Net;
using System.Net.Http.Json;
using CloudUsage.Api.Application.UsageEvents;
using CloudUsage.Api.Contracts.UsageEvents;
using CloudUsage.Api.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudUsage.Api.Tests;

public sealed class UsageEventsHttpTests
{
    private sealed class StubService : IUsageEventIngestionService
    {
        public IngestUsageEventCommand? Command { get; private set; }
        private long FixedRawEventId { get; } = 42;
        public bool Duplicate { get; init; }
        public bool Fail { get; init; }
        public Task<UsageEventIngestionResult> IngestAsync(IngestUsageEventCommand command, CancellationToken token)
        {
            Command = command;
            if (Fail) 
                throw new InvalidOperationException("Private database failure details");

            return Task.FromResult<UsageEventIngestionResult>(
                Duplicate
                ? new UsageEventIngestionResult.Duplicate(command.EventId)
                : new UsageEventIngestionResult.Created(
                    FixedRawEventId, 
                    command.EventId,
                    RawEventIngestionStatus.Pending, 
                    DateTimeOffset.Parse("2026-09-05T12:00:00Z")));
        }
    }

    private static WebApplicationFactory<Program> WebAppFactory(StubService stub) => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureLogging(
                    logging => logging.ClearProviders().AddConsole());
                builder.UseSetting(
                    "ConnectionStrings:UsageAnalyticsDatabase",
                    "Server=unused;Database=unused");
                builder.ConfigureServices(services =>
                    services.AddSingleton<IUsageEventIngestionService>(stub));
            });

    private static Dictionary<string, object?> Payload() => new()
    {
        ["eventId"] = Guid.NewGuid(), 
        ["userId"] = "user-1", 
        ["productCode"] = "studio",
        ["eventType"] = "feature_used", 
        ["occurredAtUtc"] = "2026-09-05T10:00:00Z",
        ["properties"] = new { featureName = "Analyzer" }
    };

    [Fact]
    public async Task ValidRequest_ReturnsCreatedAndMapsCommand()
    {
        var stub = new StubService();
        using var factory = WebAppFactory(stub);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/usage-events", Payload());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateUsageEventResponse>();

        Assert.Equal(42, body!.RawEventId);
        Assert.Equal("Pending", body.IngestionStatus);
        Assert.Equal(stub.Command!.EventId, body.EventId);
        Assert.Equal("user-1", stub.Command.UserExternalId);
        Assert.Contains("Analyzer", stub.Command.PropertiesJson);
    }

    [Theory]
    [InlineData("userId", null)]
    [InlineData("userId", " ")]
    [InlineData("productCode", "")]
    [InlineData("eventType", " ")]
    [InlineData("eventId", "00000000-0000-0000-0000-000000000000")]
    [InlineData("eventId", "not-a-guid")]
    [InlineData("occurredAtUtc", "not-a-date")]
    [InlineData("occurredAtUtc", null)]
    [InlineData("properties", "not-an-object")]
    public async Task InvalidRequest_Returns400WithoutCallingService(string field, string? value)
    {
        var payload = Payload();
        payload[field] = value;

        await AssertBadRequestWithoutCallingService(payload);
    }

    [Fact]
    public async Task MissingOccurredAtUtc_Returns400()
    {
        var payload = Payload();
        payload.Remove("occurredAtUtc");

        await AssertBadRequestWithoutCallingService(payload);
    }

    [Theory]
    [InlineData("userId", 129)]
    [InlineData("productCode", 65)]
    [InlineData("eventType", 65)]
    public async Task OversizedStrings_Return400(string field, int length)
    {
        var payload = Payload();
        payload[field] = new string('x', length);

        await AssertBadRequestWithoutCallingService(payload);
    }

    private static async Task AssertBadRequestWithoutCallingService(Dictionary<string, object?> payload)
    {
        var stub = new StubService();
        using var factory = WebAppFactory(stub);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/usage-events", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(stub.Command);
    }

    [Fact]
    public async Task Duplicate_Returns409()
    {
        using var factory = WebAppFactory(new StubService { Duplicate = true });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/usage-events", Payload());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedFailure_ReturnsGenericProblem()
    {
        using var factory = WebAppFactory(new StubService { Fail = true });
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsJsonAsync("/api/usage-events", Payload());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Private database", await response.Content.ReadAsStringAsync());
    }
}
