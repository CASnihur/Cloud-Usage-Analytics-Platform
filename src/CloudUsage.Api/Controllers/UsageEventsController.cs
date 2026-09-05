using System.Text.Json;
using CloudUsage.Api.Application.UsageEvents;
using CloudUsage.Api.Contracts.UsageEvents;
using Microsoft.AspNetCore.Mvc;

namespace CloudUsage.Api.Controllers;

[ApiController]
[Route("api/usage-events")]
public sealed class UsageEventsController(IUsageEventIngestionService service) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(65536)]
    [ProducesResponseType<CreateUsageEventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateUsageEventResponse>> Post(
        CreateUsageEventRequest request, CancellationToken cancellationToken)
    {
        if (request.EventId == Guid.Empty)
            ModelState.AddModelError(nameof(request.EventId), "A non-empty event ID is required.");
        if (request.Properties is { ValueKind: not JsonValueKind.Object })
            ModelState.AddModelError(nameof(request.Properties), "Properties must be a JSON object or null.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var command = new IngestUsageEventCommand(
            request.EventId, 
            request.UserId,
            request.ProductCode,
            request.EventType, 
            request.OccurredAtUtc!.Value,
            request.Properties?.GetRawText());

        var result = await service.IngestAsync(command, cancellationToken);

        return result switch
        {
            UsageEventIngestionResult.Created created => StatusCode(
                StatusCodes.Status201Created,
                new CreateUsageEventResponse(
                    created.RawEventId, 
                    created.EventId,
                    created.IngestionStatus.ToString(),
                    created.ReceivedAtUtc)),

            UsageEventIngestionResult.Duplicate => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Event already exists", 
                detail: "An event with this eventId has already been stored."),

            _ => throw new InvalidOperationException("Unknown ingestion result.")
        };
    }
}
