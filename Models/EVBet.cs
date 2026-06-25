namespace WorldCupBuddy.Models;

public enum EdgeScore
{
    None,        // gray — no value bets
    Value,       // green — some value bets
    StrongValue  // gold — strong value bets
}

/// <summary>
/// A match-level edge summary: the fixture, every comparison row, and the
/// aggregated value signals used to rank and badge the card.
/// </summary>
public class EVBet
{
    public string MatchId { get; set; } = "";
    public string HomeTeam { get; set; } = "";
    public string AwayTeam { get; set; } = "";
    public string HomeFlag { get; set; } = "🏳️";
    public string AwayFlag { get; set; } = "🏳️";
    public DateTime CommenceTime { get; set; }

    public List<OddsComparison> Rows { get; set; } = new();

    /// <summary>Highest EV available across every row/book in the match.</summary>
    public double BestEv { get; set; }

    public int ValueBetCount { get; set; }
    public int StrongValueBetCount { get; set; }

    public EdgeScore EdgeScore { get; set; } = EdgeScore.None;
}
