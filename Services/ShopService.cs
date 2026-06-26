using System.Text.Json;
using WorldCupBuddy.Models;

namespace WorldCupBuddy.Services;

/// <summary>
/// Powers the Kit Locker. When a SerpApi key is configured it returns real
/// products from Google Shopping (image, price, merchant buy-link). Otherwise it
/// falls back to gear-category tiles that deep-link to a live Google Shopping
/// search for the team.
/// </summary>
public class ShopService
{
    private const string SerpApiUrl = "https://serpapi.com/search.json";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ShopService> _logger;

    public ShopService(HttpClient http, IConfiguration config, ILogger<ShopService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public bool LiveSearchConfigured => !string.IsNullOrWhiteSpace(_config["Shopping:SerpApiKey"]);

    /// <summary>True when the last result came from live Google Shopping (vs fallback tiles).</summary>
    public bool LastUsedLiveSearch { get; private set; }

    public async Task<List<ShopProduct>> GetGearAsync(string team, CancellationToken ct = default)
    {
        team = (team ?? "").Trim();
        if (team.Length == 0) return new();

        var apiKey = _config["Shopping:SerpApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var products = await CallSerpApiAsync(team, apiKey!, ct);
                if (products is { Count: > 0 })
                {
                    LastUsedLiveSearch = true;
                    return products;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SerpApi Google Shopping request failed — using fallback tiles.");
            }
        }

        LastUsedLiveSearch = false;
        return FallbackTiles(team);
    }

    // ---- SerpApi Google Shopping ------------------------------------------

    private async Task<List<ShopProduct>?> CallSerpApiAsync(string team, string apiKey, CancellationToken ct)
    {
        var q = Uri.EscapeDataString($"{team} national team soccer jersey gear");
        var url = $"{SerpApiUrl}?engine=google_shopping&q={q}&hl=en&gl=us&num=24&api_key={apiKey}";

        using var resp = await _http.GetAsync(url, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("SerpApi {Status}: {Body}", (int)resp.StatusCode, Trunc(json));
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("shopping_results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
            return null;

        var products = new List<ShopProduct>();
        foreach (var r in results.EnumerateArray())
        {
            string S(string n) =>
                r.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";

            var title = S("title");
            var link = S("product_link");
            if (string.IsNullOrWhiteSpace(link)) link = S("link");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link)) continue;

            products.Add(new ShopProduct
            {
                Title = title,
                Price = S("price"),
                Source = S("source"),
                Thumbnail = S("thumbnail"),
                Link = link,
            });
            if (products.Count >= 24) break;
        }
        return products;
    }

    private static string Trunc(string s) => s.Length > 300 ? s[..300] : s;

    // ---- Keyless fallback: gear categories → Google Shopping search -------

    private static readonly (string label, string query, string emoji)[] Categories =
    {
        ("Home Jersey", "home jersey", "👕"),
        ("Away Jersey", "away jersey", "👕"),
        ("Cap / Hat", "cap hat", "🧢"),
        ("Scarf", "supporters scarf", "🧣"),
        ("Training Tee", "training t-shirt", "👕"),
        ("Hoodie", "hoodie", "🧥"),
        ("Soccer Ball", "soccer ball", "⚽"),
        ("Kids Kit", "kids kit youth", "🧒"),
    };

    private static List<ShopProduct> FallbackTiles(string team)
    {
        var list = new List<ShopProduct>();
        foreach (var (label, query, emoji) in Categories)
        {
            var search = Uri.EscapeDataString($"{team} national team {query}");
            list.Add(new ShopProduct
            {
                Title = $"{team} {label}",
                Emoji = emoji,
                IsSearchLink = true,
                Source = "Google Shopping",
                Link = $"https://www.google.com/search?tbm=shop&q={search}",
            });
        }
        return list;
    }
}
