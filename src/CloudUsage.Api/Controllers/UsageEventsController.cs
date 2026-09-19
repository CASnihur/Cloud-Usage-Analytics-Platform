using System.Text.Json;
using CloudUsage.Api.Application.UsageEvents;
using CloudUsage.Api.Contracts.UsageEvents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CloudUsage.Api.Controllers;

[ApiController]
[Route("api/usage-events")]
public sealed class UsageEventsController(
    IUsageEventIngestionService service,
    IOptions<JsonOptions> jsonOptions) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(65536)]
    [ProducesResponseType<CreateUsageEventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateUsageEventResponse>> Post(
        CreateUsageEventRequest request, CancellationToken cancellationToken)
    {
        ValidateCustomEventRules(request);
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

    [HttpPost("batch")]
    public Task<ActionResult<CreateUsageEventBatchResponse>> PostBatch(
        CreateUsageEventBatchRequest request, CancellationToken cancellationToken)
    {
        var results = new List<UsageEventBatchItemResult>();
        for (var index = 0; index < request.Events!.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eventRequest = ParseAndValidateBatchItem(request.Events[index]);
            if (!ModelState.IsValid)
            {
                // Copy errors before the next item clears ModelState.
                var errors = ModelState
                    .Where(entry => entry.Value!.Errors.Count > 0)
                    .ToDictionary(entry => entry.Key,
                        entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());
                results.Add(new UsageEventBatchItemResult(index, StatusCodes.Status400BadRequest,
                    null, errors, null));
                continue;
            }

            // Next learning step: ingest eventRequest and map Created/Duplicate to a result.
            throw new NotImplementedException("Valid batch event ingestion is not implemented yet.");
        }

        return Task.FromResult<ActionResult<CreateUsageEventBatchResponse>>(
            Ok(new CreateUsageEventBatchResponse(results)));
    }

    private CreateUsageEventRequest? ParseAndValidateBatchItem(JsonElement item)
    {
        ModelState.Clear();
        if (item.ValueKind != JsonValueKind.Object)
        {
            ModelState.AddModelError("$", "Each event must be a JSON object.");
            return null;
        }

        CreateUsageEventRequest? eventRequest;
        try
        {
            eventRequest = item.Deserialize<CreateUsageEventRequest>(jsonOptions.Value.JsonSerializerOptions);
        }
        catch (JsonException exception)
        {
            ModelState.AddModelError(exception.Path ?? "$", "The JSON value is invalid for this field.");
            return null;
        }

        if (eventRequest is null)
        {
            ModelState.AddModelError("$", "Each event must be a JSON object.");
            return null;
        }

        TryValidateModel(eventRequest);
        ValidateCustomEventRules(eventRequest);
        return eventRequest;
    }

    private void ValidateCustomEventRules(CreateUsageEventRequest request)
    {
        if (request.EventId == Guid.Empty)
            ModelState.AddModelError(nameof(request.EventId), "A non-empty event ID is required.");

        if (request.Properties is { ValueKind: not JsonValueKind.Object })
            ModelState.AddModelError(nameof(request.Properties), "Properties must be a JSON object or null.");
    }
}
