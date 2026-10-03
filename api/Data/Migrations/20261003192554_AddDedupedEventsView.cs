using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SitePulse.Api.Data.Migrations
{
    /// <summary>
    /// The one place where the duplicate rule lives. The seed contains rows that are identical in
    /// every column except id (double ingestion). The tables keep them as delivered; reads go through
    /// this view, which collapses rows identical in every column but id.
    /// </summary>
    public partial class AddDedupedEventsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIEW activity_events_deduped AS
                SELECT DISTINCT account_id, location, event_type, occurred_at, duration_seconds, outcome
                FROM activity_events;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW activity_events_deduped;");
        }
    }
}
