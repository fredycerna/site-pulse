namespace SitePulse.Api.Baseline;

/// <summary>The demo clock: the seed is a snapshot ending 2026-07-27, so "now" is pinned there (Dashboard:NowOverrideUtc).</summary>
public sealed class PinnedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
