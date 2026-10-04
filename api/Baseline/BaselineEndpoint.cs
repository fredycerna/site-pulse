using System.Globalization;

namespace SitePulse.Api.Baseline;

public static class BaselineEndpoint
{
    public static IEndpointRouteBuilder MapActivityBaseline(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounts/{accountId:int}/activity-baseline", async (
            int accountId, string? eventType, string? week, BaselineService service, CancellationToken ct) =>
        {
            var (type, requestedWeek, error) = ParseQuery(eventType, week);
            if (error is not null) return BadRequest(error);

            return await service.GetAsync(accountId, type, requestedWeek, ct) switch
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

    /// <summary>Input validation for the query string. Whether the week has ended depends on the account's timezone, so the service checks that.</summary>
    public static (string EventType, DateOnly? Week, string? Error) ParseQuery(string? eventType, string? week)
    {
        eventType ??= EventTypes.All;
        if (!EventTypes.Accepted.Contains(eventType))
            return (eventType, null, $"eventType must be one of: {string.Join(", ", EventTypes.Accepted)}.");
        if (week is null) return (eventType, null, null);
        if (!DateOnly.TryParseExact(week, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return (eventType, null, "week must be a date in the form YYYY-MM-DD (the local Monday that starts the week).");
        return (eventType, parsed, null);
    }

    private static IResult BadRequest(string detail) =>
        Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "Invalid request");
}
