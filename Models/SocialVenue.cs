namespace WorldCupBuddy.Models;

/// <summary>
/// A place to watch the game, suggested by Claude for the user's city and vibe.
/// </summary>
public class SocialVenue
{
    public string Name { get; set; } = "";
    public string VenueType { get; set; } = "";     // Sports Bar, Pub, Restaurant, Brewery…
    public string Neighborhood { get; set; } = "";
    public string Address { get; set; } = "";        // street address or area (approximate)
    public string Why { get; set; } = "";            // why it's good for watching the game
    public string Emoji { get; set; } = "📺";
    public string City { get; set; } = "";

    /// <summary>The query used for the Google Maps embed — most specific first.</summary>
    public string MapQuery =>
        string.Join(", ", new[] { Name, Address, Neighborhood, City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
