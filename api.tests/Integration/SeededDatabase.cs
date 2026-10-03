using Microsoft.EntityFrameworkCore;
using SitePulse.Api.Data;

namespace SitePulse.Api.Tests.Integration;

/// <summary>
/// Integration tests read the database from docker-compose.yml, already migrated and seeded
/// (`dotnet run --project api -- seed`). No extra containers, no test-only database.
/// Override the connection with SITEPULSE_TEST_CONNECTION.
/// </summary>
public static class SeededDatabase
{
    public const string DefaultConnection =
        "Server=localhost,1433;Database=SitePulse;User Id=sa;Password=SitePulse_Dev_Only_2026!;TrustServerCertificate=True;Encrypt=True";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("SITEPULSE_TEST_CONNECTION") ?? DefaultConnection;

    public static string SeedPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../db/seed/seed.sql"));

    public static async Task<SitePulseDbContext> OpenAsync()
    {
        var db = new SitePulseDbContext(new DbContextOptionsBuilder<SitePulseDbContext>()
            .UseSqlServer(ConnectionString).Options);
        if (!await db.Database.CanConnectAsync())
        {
            await db.DisposeAsync();
            throw new InvalidOperationException(
                "The seeded database is not reachable. Run `docker compose up -d` and " +
                "`dotnet run --project api -- seed` before `dotnet test`.");
        }
        return db;
    }
}
