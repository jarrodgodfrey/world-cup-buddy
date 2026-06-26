using System.Text;
using System.Text.Json;
using WorldCupBuddy.Models;

namespace WorldCupBuddy.Services;

/// <summary>
/// Finds places to watch the game in a given city using Claude, tailored to the
/// fan's preferred vibe. Falls back to generic suggestions when no API key is set.
/// </summary>
public class SocialService
{
    private const string ModelId = "claude-opus-4-8";
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<SocialService> _logger;

    public SocialService(HttpClient http, IConfiguration config, ILogger<SocialService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public bool AiConfigured => !string.IsNullOrWhiteSpace(_config["Anthropic:ApiKey"]);

    /// <summary>True when the most recent search used Claude (vs the local fallback).</summary>
    public bool LastUsedAi { get; private set; }

    /// <summary>
    /// Suggest watch-party venues in <paramref name="city"/>, biased toward the
    /// fan's <paramref name="vibe"/> if provided.
    /// </summary>
    public async Task<List<SocialVenue>> FindVenuesAsync(
        string city, string? vibe = null, string? team = null, CancellationToken ct = default)
    {
        city = (city ?? "").Trim();
        if (city.Length == 0) return new();

        var apiKey = _config["Anthropic:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var venues = await CallClaudeAsync(city, vibe, team, apiKey!, ct);
                if (venues is { Count: > 0 })
                {
                    LastUsedAi = true;
                    foreach (var v in venues) v.City = city;
                    return venues;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claude venue search failed — using fallback.");
            }
        }

        LastUsedAi = false;
        var fb = FallbackVenues(city, vibe);
        foreach (var v in fb) v.City = city;
        return fb;
    }

    // ---- Claude (forced tool call) ----------------------------------------

    private async Task<List<SocialVenue>?> CallClaudeAsync(
        string city, string? vibe, string? team, string apiKey, CancellationToken ct)
    {
        var ask = $"Recommend 6 real bars or restaurants in {city} that are great places to watch a " +
                  "World Cup / soccer match — places with good screens and a lively game-day atmosphere.";
        if (!string.IsNullOrWhiteSpace(vibe))
            ask += $" The fan's preferred vibe is: \"{vibe}\" — lean toward spots matching that.";
        if (!string.IsNullOrWhiteSpace(team))
            ask += $" Bonus if any are known {team} supporters' bars.";

        var requestBody = new
        {
            model = ModelId,
            max_tokens = 1500,
            system =
                "You are a local guide for sports fans. Recommend real, well-known bars and restaurants " +
                "that are good for watching live soccer. Prefer genuinely popular, real venues in the city. " +
                "For each, give the name, venue type, neighborhood, an approximate street address or area, " +
                "and one short sentence on why it's a great spot to watch the game. Pick an apt emoji per venue.",
            tools = new object[]
            {
                new
                {
                    name = "save_venues",
                    description = "Save the list of recommended watch-party venues.",
                    input_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            venues = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new { type = "string" },
                                        venue_type = new { type = "string" },
                                        neighborhood = new { type = "string" },
                                        address = new { type = "string" },
                                        why = new { type = "string" },
                                        emoji = new { type = "string" }
                                    },
                                    required = new[] { "name", "venue_type", "why" }
                                }
                            }
                        },
                        required = new[] { "venues" }
                    }
                }
            },
            tool_choice = new { type = "tool", name = "save_venues" },
            messages = new object[] { new { role = "user", content = ask } }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Anthropic API {Status}: {Body}", (int)resp.StatusCode, json);
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "tool_use" &&
                block.TryGetProperty("input", out var input) &&
                input.TryGetProperty("venues", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                var list = new List<SocialVenue>();
                foreach (var v in arr.EnumerateArray())
                {
                    string S(string n) =>
                        v.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";
                    var name = S("name");
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var emoji = S("emoji");
                    list.Add(new SocialVenue
                    {
                        Name = name,
                        VenueType = S("venue_type"),
                        Neighborhood = S("neighborhood"),
                        Address = S("address"),
                        Why = S("why"),
                        Emoji = string.IsNullOrWhiteSpace(emoji) ? "📺" : emoji,
                    });
                }
                return list;
            }
        }
        return null;
    }

    // ---- Fallback ---------------------------------------------------------

    private static List<SocialVenue> FallbackVenues(string city, string? vibe)
    {
        var typedVibe = string.IsNullOrWhiteSpace(vibe) ? "a lively game-day crowd" : vibe!.ToLowerInvariant();
        return new()
        {
            new() { Name = "The Local Sports Bar", VenueType = "Sports Bar", Neighborhood = "Downtown",
                    Why = $"Wall-to-wall screens and {typedVibe} — a safe bet for any match.", Emoji = "🍺" },
            new() { Name = "Corner Pub", VenueType = "Pub", Neighborhood = "City Center",
                    Why = "Classic pub atmosphere with the game on every TV.", Emoji = "🍻" },
            new() { Name = "Stadium Taproom", VenueType = "Brewery", Neighborhood = "Riverside",
                    Why = "Big projector screen and a packed, energetic crowd on match days.", Emoji = "⚽" },
            new() { Name = "Kickoff Cantina", VenueType = "Restaurant", Neighborhood = "Midtown",
                    Why = "Food, drinks, and sound turned up for the big games.", Emoji = "🌮" },
        };
    }
}
