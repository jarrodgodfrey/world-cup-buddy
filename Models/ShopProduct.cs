namespace WorldCupBuddy.Models;

/// <summary>
/// A purchasable item in the Kit Locker. Either a real product (from SerpApi's
/// Google Shopping results) or, in keyless fallback mode, a gear category that
/// links out to a live Google Shopping search.
/// </summary>
public class ShopProduct
{
    public string Title { get; set; } = "";
    public string Price { get; set; } = "";
    public string Source { get; set; } = "";       // merchant / store name
    public string Thumbnail { get; set; } = "";     // image URL (empty for fallback tiles)
    public string Link { get; set; } = "";          // where "Buy" sends the user
    public string Emoji { get; set; } = "🛍️";       // shown when there's no thumbnail

    /// <summary>True for keyless fallback tiles that open a Google Shopping search.</summary>
    public bool IsSearchLink { get; set; }
}
