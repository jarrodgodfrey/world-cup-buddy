namespace WorldCupBuddy.Models;

/// <summary>
/// A watch-party venue. Used only by the stubbed Social feature for the
/// blurred preview mockup. Real geolocation data is not yet wired up.
/// </summary>
public class SocialVenue
{
    public string Name { get; set; } = "";
    public string VenueType { get; set; } = "";   // e.g. "Sports Bar", "Brewery"
    public double DistanceMiles { get; set; }
    public int WatchingCount { get; set; }
    public string ShowingMatch { get; set; } = "";
    public double Rating { get; set; }
    public string Emoji { get; set; } = "🍺";
}
