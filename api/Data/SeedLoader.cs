using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SitePulse.Api.Data;

/// <summary>
/// Loads db/seed/seed.sql exactly as provided. The file is never edited or cleaned:
/// messy rows are handled at read time. Runs in one transaction, so it is all or nothing,
/// and it is a no-op when accounts already exist.
/// </summary>
public class SeedLoader(SitePulseDbContext db, ILogger<SeedLoader> logger)
{
    public const int DefaultBatchSize = 500;

    /// <returns>Number of statements executed (0 when the database was already seeded).</returns>
    public async Task<int> LoadAsync(string seedPath, int batchSize = DefaultBatchSize, CancellationToken ct = default)
    {
        if (await db.Accounts.AnyAsync(ct))
        {
            logger.LogInformation("Seed skipped: accounts table is not empty");
            return 0;
        }

        // The seed has one statement per line; comments and blank lines are the only other content.
        var statements = (await File.ReadAllLinesAsync(seedPath, ct))
            .Where(line => line.Length > 0 && !line.StartsWith("--", StringComparison.Ordinal))
            .ToList();

        var started = System.Diagnostics.Stopwatch.StartNew();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var connection = db.Database.GetDbConnection();

        foreach (var batch in statements.Chunk(batchSize))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = tx.GetDbTransaction();
            command.CommandTimeout = 120;
            command.CommandText = string.Join('\n', batch);
            await command.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        logger.LogInformation("Seed loaded: {Statements} statements in {ElapsedMs} ms", statements.Count, started.ElapsedMilliseconds);
        return statements.Count;
    }
}
