namespace WorldCupBuddy.Models;

/// <summary>Aggregated insights from a head-to-head Monte Carlo match simulation.</summary>
public class MatchPrediction
{
    public string TeamA { get; set; } = "";
    public string FlagA { get; set; } = "🏳️";
    public int RatingA { get; set; }

    public string TeamB { get; set; } = "";
    public string FlagB { get; set; } = "🏳️";
    public int RatingB { get; set; }

    public int Iterations { get; set; }
    public DateTime RunAt { get; set; }

    // Regulation result (90 minutes, draw allowed) — percentages 0–100.
    public double WinPctA { get; set; }
    public double DrawPct { get; set; }
    public double WinPctB { get; set; }

    // "Fair" moneyline odds (American) derived from the regulation probabilities.
    public string FairOddsA { get; set; } = "—";
    public string FairOddsDraw { get; set; } = "—";
    public string FairOddsB { get; set; } = "—";

    // Knockout view (a winner is required — extra time / penalties).
    public double AdvancePctA { get; set; }
    public double AdvancePctB { get; set; }

    // Goals & markets.
    public double AvgGoalsA { get; set; }
    public double AvgGoalsB { get; set; }
    public double AvgTotalGoals => AvgGoalsA + AvgGoalsB;
    public double Over25Pct { get; set; }
    public double Under25Pct { get; set; }
    public double BttsPct { get; set; }          // both teams to score
    public double CleanSheetPctA { get; set; }
    public double CleanSheetPctB { get; set; }

    public List<ScorelineProb> TopScorelines { get; set; } = new();

    /// <summary>A one-line plain-English read of the matchup.</summary>
    public string Narrative { get; set; } = "";
}

public class ScorelineProb
{
    public string Score { get; set; } = "";   // "2–1" from Team A's perspective
    public double Pct { get; set; }
}
