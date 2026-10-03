using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SitePulse.Api.Data;

namespace SitePulse.Api.Tests.Integration;

public class SeedLoaderTests
{
    [Fact]
    public async Task Seeded_database_holds_the_starter_data_as_delivered()
    {
        await using var db = await SeededDatabase.OpenAsync();

        Assert.Equal(20, await db.Accounts.CountAsync());
        Assert.Equal(12626, await db.ActivityEvents.CountAsync());
    }

    [Fact]
    public async Task Loading_again_is_a_no_op()
    {
        await using var db = await SeededDatabase.OpenAsync();
        var loader = new SeedLoader(db, NullLogger<SeedLoader>.Instance);

        var executed = await loader.LoadAsync(SeededDatabase.SeedPath);

        Assert.Equal(0, executed);
        Assert.Equal(12626, await db.ActivityEvents.CountAsync());
    }

    [Fact]
    public async Task Deduped_view_drops_exactly_the_twelve_double_ingested_rows()
    {
        await using var db = await SeededDatabase.OpenAsync();

        var deduped = await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM activity_events_deduped")
            .SingleAsync();

        Assert.Equal(12626 - 12, deduped);
    }
}
