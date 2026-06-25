namespace WorldCupBuddy.Models;

/// <summary>
/// One comparison row in the Edge Finder table: a single selection (e.g. "Home Win")
/// with the sharp Pinnacle line plus the three public books and their expected value.
/// </summary>
public class OddsComparison
{
    /// <summary>Display label: "Home Win", "Draw", "Away Win", "Over 2.5", "Under 2.5".</summary>
    public string Selection { get; set; } = "";

    /// <summary>Market this row belongs to: "h2h" or "totals".</summary>
    public string Market { get; set; } = "";

    // Sharp reference line.
    public double? PinnacleAmerican { get; set; }

    /// <summary>Vig-removed Pinnacle probability used as the "true" probability.</summary>
    public double PinnacleTrueProb { get; set; }

    // Public book American odds (null when the book doesn't offer the line).
    public double? DraftKingsAmerican { get; set; }
    public double? FanDuelAmerican { get; set; }
    public double? BetMgmAmerican { get; set; }

    // Expected value per book (decimal, e.g. 0.05 == +5%).
    public double? DraftKingsEv { get; set; }
    public double? FanDuelEv { get; set; }
    public double? BetMgmEv { get; set; }

    /// <summary>The best (highest) EV available across the three public books.</summary>
    public double BestEv =>
        new[] { DraftKingsEv, FanDuelEv, BetMgmEv }
            .Where(e => e.HasValue)
            .Select(e => e!.Value)
            .DefaultIfEmpty(double.NegativeInfinity)
            .Max();

    public bool HasAnyValue => BestEv > OddsThresholds.Value;
    public bool HasStrongValue => BestEv > OddsThresholds.StrongValue;
}

public static class OddsThresholds
{
    public const double Value = 0.03;       // +3% edge
    public const double StrongValue = 0.07; // +7% edge
}
