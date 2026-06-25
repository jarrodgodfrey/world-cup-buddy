namespace WorldCupBuddy.Models;

/// <summary>
/// A single World Cup fixture with the full set of bookmaker lines attached.
/// Mirrors the shape returned by The Odds API after deserialization.
/// </summary>
public class Match
{
    public string Id { get; set; } = "";
    public string HomeTeam { get; set; } = "";
    public string AwayTeam { get; set; } = "";
    public DateTime CommenceTime { get; set; }
    public List<BookmakerOdds> Bookmakers { get; set; } = new();

    public BookmakerOdds? Book(string key) =>
        Bookmakers.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase));
}

public class BookmakerOdds
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public List<MarketOdds> Markets { get; set; } = new();

    public MarketOdds? Market(string key) =>
        Markets.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
}

public class MarketOdds
{
    /// <summary>"h2h" (match winner) or "totals" (over/under goals).</summary>
    public string Key { get; set; } = "";
    public List<OutcomeOdds> Outcomes { get; set; } = new();
}

public class OutcomeOdds
{
    public string Name { get; set; } = "";

    /// <summary>American odds (positive or negative).</summary>
    public double Price { get; set; }

    /// <summary>The total line for over/under markets (e.g. 2.5).</summary>
    public double? Point { get; set; }
}
