using Microsoft.EntityFrameworkCore;
using SitePulse.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<SitePulseDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SitePulse")
        ?? throw new InvalidOperationException("Connection string 'SitePulse' is not configured.")));
builder.Services.AddScoped<SeedLoader>();

var app = builder.Build();

var seedPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath,
    app.Configuration["Seed:Path"] ?? "../db/seed/seed.sql"));

// `dotnet run --project api -- seed`: apply migrations, load the seed if empty, exit.
if (args.Contains("seed"))
{
    await MigrateAndSeedAsync(app.Services, seedPath);
    return;
}

if (app.Environment.IsDevelopment())
{
    await MigrateAndSeedAsync(app.Services, seedPath);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/health", async (SitePulseDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok" })
        : Results.Problem("Database is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapGet("/api/accounts", async (SitePulseDbContext db, CancellationToken ct) =>
    await db.Accounts.AsNoTracking()
        .OrderBy(a => a.Id)
        .Select(a => new AccountSummary(a.Id, a.Name, a.Timezone))
        .ToListAsync(ct));

app.Run();

static async Task MigrateAndSeedAsync(IServiceProvider services, string seedPath)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SitePulseDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<SeedLoader>().LoadAsync(seedPath);
}

public record AccountSummary(int Id, string Name, string Timezone);

public partial class Program;
