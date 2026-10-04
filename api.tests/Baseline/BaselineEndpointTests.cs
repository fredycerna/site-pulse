using SitePulse.Api.Baseline;

namespace SitePulse.Api.Tests.Baseline;

public class BaselineEndpointTests
{
    [Theory]
    [InlineData("foo")]
    [InlineData("ALL")]
    [InlineData("call_received;DROP TABLE accounts")]
    public void Event_type_outside_the_whitelist_is_rejected(string eventType)
    {
        Assert.NotNull(BaselineEndpoint.ParseQuery(eventType, null).Error);
    }

    [Theory]
    [InlineData("junk")]
    [InlineData("2026-7-20")]
    [InlineData("20/07/2026")]
    public void Week_that_is_not_an_iso_date_is_rejected(string week)
    {
        Assert.NotNull(BaselineEndpoint.ParseQuery("all", week).Error);
    }

    [Fact]
    public void Defaults_to_all_and_the_last_complete_week()
    {
        Assert.Equal(("all", (DateOnly?)null, (string?)null), BaselineEndpoint.ParseQuery(null, null));
        Assert.Equal(("lead_created", (DateOnly?)new DateOnly(2026, 7, 20), (string?)null),
            BaselineEndpoint.ParseQuery("lead_created", "2026-07-20"));
    }
}
