namespace WorldCupBuddy.Models;

/// <summary>The full result of a Monte Carlo run.</summary>
public class BracketSimulation
{
    public List<TeamSimResult> Teams { get; set; } = new();
    public DateTime RunAt { get; set; }
    public int Iterations { get; set; }

    /// <summary>The deep-dive path for the user's selected team (null if none chosen).</summary>
    public TeamPath? SelectedPath { get; set; }
}

/// <summary>The selected team's projected route through the knockout rounds.</summary>
public class TeamPath
{
    public string Team { get; set; } = "";
    public string Flag { get; set; } = "🏳️";
    public int Rating { get; set; }
    public string Group { get; set; } = "";
    public List<PathNode> Nodes { get; set; } = new();
    public string Narrative { get; set; } = "";
}

public class PathNode
{
    public string Round { get; set; } = "";        // R16, QF, SF, Final, Champion
    public double ReachProb { get; set; }           // percentage (0–100)
    public string LikelyOpponent { get; set; } = "—";
    public string LikelyOpponentFlag { get; set; } = "";
}
