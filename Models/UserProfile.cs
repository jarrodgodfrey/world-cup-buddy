namespace WorldCupBuddy.Models;

/// <summary>
/// A fan profile built from a spoken (or typed) self-description. Powers the
/// personalized experience: Edge Finder filters to the favorite team, the
/// Simulator auto-runs it.
/// </summary>
public class UserProfile
{
    public string FavoriteTeam { get; set; } = "";
    public List<string> FavoritePlayers { get; set; } = new();
    public string Location { get; set; } = "";
    public string BarVibe { get; set; } = "";
    public string BettingStyle { get; set; } = "";

    /// <summary>"Low", "Medium", or "High".</summary>
    public string RiskTolerance { get; set; } = "Medium";

    /// <summary>A short, friendly one-line recap of the fan.</summary>
    public string Summary { get; set; } = "";

    /// <summary>The raw transcript the profile was built from.</summary>
    public string SourceTranscript { get; set; } = "";

    public string Flag => WorldCupData.Flag(FavoriteTeam);

    /// <summary>True when the favorite team is one of the 32 simulated nations.</summary>
    public bool TeamIsInField => WorldCupData.Find(FavoriteTeam) is not null;

    public bool HasTeam => !string.IsNullOrWhiteSpace(FavoriteTeam);
}
