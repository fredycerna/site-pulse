namespace SitePulse.Api.Data;

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Industry { get; set; } = "";

    /// <summary>IANA timezone id, e.g. "America/Chicago".</summary>
    public string Timezone { get; set; } = "";

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
