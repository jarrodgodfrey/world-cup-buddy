using WorldCupBuddy.Models;

namespace WorldCupBuddy.Services;

/// <summary>
/// STUB. The Watch Party Finder is not yet implemented. This service exposes the
/// method surface the real feature will use, plus sample venues for the blurred
/// preview mockup on the Social page.
/// </summary>
public class SocialService
{
    // TODO: Inject a geocoding/places provider (Google Places, Foursquare, etc.)
    //       and a backing store for user-created watch parties.

    /// <summary>
    /// TODO: Return real venues near the supplied coordinates that are showing
    /// the given match. For now this returns the mock preview set.
    /// </summary>
    public Task<List<SocialVenue>> FindNearbyVenuesAsync(
        double latitude, double longitude, double radiusMiles = 5.0,
        CancellationToken ct = default)
    {
        // TODO: real lookup. Coordinates currently ignored.
        return Task.FromResult(PreviewVenues());
    }

    /// <summary>
    /// TODO: Persist a user-hosted watch party and broadcast it to nearby fans.
    /// </summary>
    public Task<SocialVenue> CreateWatchPartyAsync(SocialVenue party, CancellationToken ct = default)
    {
        // TODO: validate, persist, and return the saved entity with a real id.
        throw new NotImplementedException("Watch Party creation is coming soon.");
    }

    /// <summary>
    /// TODO: Mark the current user as attending a venue's watch party.
    /// </summary>
    public Task JoinWatchPartyAsync(string venueId, CancellationToken ct = default)
    {
        // TODO: record attendance.
        throw new NotImplementedException("Joining watch parties is coming soon.");
    }

    /// <summary>Sample data for the "Coming Soon" preview mockup only.</summary>
    public List<SocialVenue> PreviewVenues() => new()
    {
        new() { Name = "The Offside Tavern", VenueType = "Sports Bar",  DistanceMiles = 0.4, WatchingCount = 84, ShowingMatch = "USA vs Mexico",      Rating = 4.7, Emoji = "🍺" },
        new() { Name = "Stadium Brewing Co.", VenueType = "Brewery",     DistanceMiles = 1.1, WatchingCount = 52, ShowingMatch = "Brazil vs Iran",      Rating = 4.5, Emoji = "🍻" },
        new() { Name = "Corner Kick Cantina", VenueType = "Restaurant",  DistanceMiles = 1.8, WatchingCount = 37, ShowingMatch = "Argentina vs Canada", Rating = 4.6, Emoji = "🌮" },
        new() { Name = "The Golden Boot",      VenueType = "Pub",         DistanceMiles = 2.3, WatchingCount = 119, ShowingMatch = "England vs Serbia",  Rating = 4.8, Emoji = "⚽" },
    };
}
