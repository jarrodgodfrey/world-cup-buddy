namespace WorldCupBuddy.Models;

/// <summary>
/// Aggregated Monte Carlo results for a single team across all simulations.
/// All probability fields are percentages (0–100).
/// </summary>
public class TeamSimResult
{
    public string Team { get; set; } = "";
    public string Flag { get; set; } = "🏳️";
    public string Group { get; set; } = "";
    public int Rating { get; set; }

    /// <summary>Chance the team is eliminated in the group stage (does not advance).</summary>
    public double GroupExitPct { get; set; }

    public double R16Pct { get; set; }    // reached the Round of 16
    public double QfPct { get; set; }     // reached the Quarter-finals
    public double SfPct { get; set; }     // reached the Semi-finals
    public double FinalPct { get; set; }  // reached the Final
    public double WinPct { get; set; }    // won the tournament
}
