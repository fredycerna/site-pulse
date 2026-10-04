using System.Globalization;

namespace SitePulse.Api.Baseline;

public static class BaselineEndpoint
{
    public static IEndpointRouteBuilder MapActivityBaseline(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounts/{accountId:int}/activity-baseline", async (
            int accountId, string? eventType, string? week, BaselineService service, CancellationToken ct) =>
        {
            eventType ??= EventTypes.All;
            if (!EventTypes.Accepted.Contains(eventType))
                return BadRequest($"eventType must be one of: {string.Join(", ", EventTypes.Accepted)}.");

            DateOnly? requestedWeek = null;
            if (week is not null)
            {
                if (!DateOnly.TryParseExact(week, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    return BadRequest("week must be a date in the form YYYY-MM-DD (the local Monday that starts the week).");
                requestedWeek = parsed;
            }

            return await service.GetAsync(accountId, eventType, requestedWeek, ct) switch
            {
                BaselineOutcome.Found found => Results.Ok(found.Response),
                BaselineOutcome.AccountNotFound => Results.Problem(
                    $"Account {accountId} does not exist.", statusCode: StatusCodes.Status404NotFound, title: "Unknown account"),
                BaselineOutcome.InvalidWeek invalid => BadRequest(invalid.Reason),
                BaselineOutcome.UnusableTimezone tz => Results.Problem(
                    $"The account's timezone '{tz.Timezone}' is not a known IANA zone, so its weeks cannot be computed.",
                    statusCode: StatusCodes.Status422UnprocessableEntity, title: "Unusable account data"),
                var other => throw new InvalidOperationException($"Unhandled outcome {other.GetType().Name}."),
            };
        });
        return app;
    }

    private static IResult BadRequest(string detail) =>
        Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "Invalid request");
}
