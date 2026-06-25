using System.Text;
using System.Text.Json;
using WorldCupBuddy.Models;

namespace WorldCupBuddy.Services;

/// <summary>
/// Turns a free-form spoken/typed self-description into a structured
/// <see cref="UserProfile"/>. Primary path calls Claude (Anthropic Messages API)
/// with a forced tool call so the model returns schema-valid JSON. Falls back to
/// a keyword parser when no API key is configured or the call fails.
/// </summary>
public class ProfileService
{
    private const string Model = "claude-opus-4-8";
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(HttpClient http, IConfiguration config, ILogger<ProfileService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public bool AiConfigured =>
        !string.IsNullOrWhiteSpace(_config["Anthropic:ApiKey"]);

    /// <summary>True when the most recent build used Claude (vs the local fallback).</summary>
    public bool LastUsedAi { get; private set; }

    public async Task<UserProfile> BuildProfileAsync(string transcript, CancellationToken ct = default)
    {
        transcript = (transcript ?? "").Trim();
        if (transcript.Length == 0)
            return new UserProfile { Summary = "Tell me about yourself to build a profile." };

        var apiKey = _config["Anthropic:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var profile = await CallClaudeAsync(transcript, apiKey!, ct);
                if (profile is not null)
                {
                    LastUsedAi = true;
                    profile.SourceTranscript = transcript;
                    return profile;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claude profile extraction failed — using keyword fallback.");
            }
        }

        LastUsedAi = false;
        var fallback = HeuristicParse(transcript);
        fallback.SourceTranscript = transcript;
        return fallback;
    }

    // ---- Claude (forced tool call) ----------------------------------------

    private async Task<UserProfile?> CallClaudeAsync(string transcript, string apiKey, CancellationToken ct)
    {
        var teamList = string.Join(", ", WorldCupData.Teams.Select(t => t.Name));

        var requestBody = new
        {
            model = Model,
            max_tokens = 1024,
            system =
                "You build a World Cup fan profile from how a fan describes themselves. " +
                "Extract their favorite national team, favorite players, where they live, the kind of " +
                "bar/venue vibe they enjoy watching games at, their betting style, and risk tolerance. " +
                "If the favorite team is one of these World Cup nations, use that exact spelling: " +
                teamList + ". " +
                "risk_tolerance must be exactly Low, Medium, or High. " +
                "Write a short, energetic one-sentence summary of the fan.",
            tools = new object[]
            {
                new
                {
                    name = "save_profile",
                    description = "Save the structured fan profile extracted from the description.",
                    input_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            favorite_team = new { type = "string", description = "National team name." },
                            favorite_players = new { type = "array", items = new { type = "string" } },
                            location = new { type = "string", description = "City or place they live, if mentioned." },
                            bar_vibe = new { type = "string", description = "Preferred watch-party vibe, e.g. 'Sports bar with friends'." },
                            betting_style = new { type = "string", description = "e.g. 'Value bettor', 'Casual', 'High roller'." },
                            risk_tolerance = new { type = "string", @enum = new[] { "Low", "Medium", "High" } },
                            summary = new { type = "string" }
                        },
                        required = new[] { "favorite_team", "risk_tolerance", "summary" }
                    }
                }
            },
            tool_choice = new { type = "tool", name = "save_profile" },
            messages = new object[]
            {
                new { role = "user", content = transcript }
            }
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
        // Find the forced tool_use block and read its `input`.
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "tool_use" &&
                block.TryGetProperty("input", out var input))
            {
                return MapInput(input);
            }
        }
        return null;
    }

    private static UserProfile MapInput(JsonElement input)
    {
        string Str(string name) =>
            input.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? "" : "";

        var players = new List<string>();
        if (input.TryGetProperty("favorite_players", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var p in arr.EnumerateArray())
                if (p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString()))
                    players.Add(p.GetString()!);

        var risk = Str("risk_tolerance");
        risk = risk is "Low" or "Medium" or "High" ? risk : "Medium";

        return new UserProfile
        {
            FavoriteTeam = ResolveTeam(Str("favorite_team")),
            FavoritePlayers = players,
            Location = Str("location"),
            BarVibe = Str("bar_vibe"),
            BettingStyle = Str("betting_style"),
            RiskTolerance = risk,
            Summary = Str("summary"),
        };
    }

    /// <summary>Snap a model-provided team name to the canonical field spelling when possible.</summary>
    private static string ResolveTeam(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var match = WorldCupData.Teams.FirstOrDefault(t =>
            t.Name.Equals(raw, StringComparison.OrdinalIgnoreCase) ||
            raw.Contains(t.Name, StringComparison.OrdinalIgnoreCase));
        return match?.Name ?? raw.Trim();
    }

    // ---- Keyword fallback -------------------------------------------------

    private static readonly string[] KnownPlayers =
    {
        "Vinicius Jr", "Vinicius", "Vini", "Messi", "Mbappe", "Mbappé", "Haaland", "Neymar",
        "Ronaldo", "Bellingham", "Kane", "Pulisic", "Musiala", "Modric", "Lautaro", "Rodrygo",
        "Son", "Salah", "Griezmann", "Pedri", "Gavi", "Yamal", "Foden", "Saka",
    };

    private UserProfile HeuristicParse(string transcript)
    {
        var lower = transcript.ToLowerInvariant();

        // Favorite team — first known nation mentioned.
        var team = WorldCupData.Teams
            .FirstOrDefault(t => lower.Contains(t.Name.ToLowerInvariant()))?.Name ?? "";

        // Players — scan known stars (dedupe Vinicius variants).
        var players = new List<string>();
        foreach (var p in KnownPlayers)
            if (lower.Contains(p.ToLowerInvariant()) &&
                !players.Any(x => x.StartsWith(p.Split(' ')[0], StringComparison.OrdinalIgnoreCase)))
                players.Add(p.StartsWith("Vini", StringComparison.OrdinalIgnoreCase) ? "Vinicius Jr" : p);

        // Risk tolerance.
        var risk = "Medium";
        if (lower.Contains("don't want to risk") || lower.Contains("not too much") ||
            lower.Contains("too much") || lower.Contains("safe") || lower.Contains("low risk") ||
            lower.Contains("conservative") || lower.Contains("careful"))
            risk = "Low";
        else if (lower.Contains("high roller") || lower.Contains("big bet") || lower.Contains("go big") ||
                 lower.Contains("love risk") || lower.Contains("aggressive"))
            risk = "High";

        // Betting style.
        var style = "Casual fan";
        if (lower.Contains("value")) style = "Value bettor";
        else if (lower.Contains("parlay")) style = "Parlay player";
        else if (lower.Contains("high roller")) style = "High roller";
        else if (lower.Contains("bet")) style = "Recreational bettor";

        // Bar vibe.
        var vibe = "";
        if (lower.Contains("sports bar")) vibe = lower.Contains("friend") ? "Sports bar with friends" : "Sports bar";
        else if (lower.Contains("pub")) vibe = "Local pub";
        else if (lower.Contains("home") || lower.Contains("couch")) vibe = "At home on the couch";
        else if (lower.Contains("brewery")) vibe = "Craft brewery";
        else if (lower.Contains("quiet")) vibe = "Somewhere quiet";
        else if (lower.Contains("friend")) vibe = "Watching with friends";

        // Location — "in/from <City>".
        var location = "";
        var m = System.Text.RegularExpressions.Regex.Match(
            transcript, @"\b(?:in|from|live in|based in)\s+([A-Z][a-zA-Z]+(?:\s[A-Z][a-zA-Z]+)?)");
        if (m.Success) location = m.Groups[1].Value;

        var flag = WorldCupData.Flag(team);
        var summary = team.Length > 0
            ? $"{flag} A passionate {team} fan{(players.Count > 0 ? $" who loves {players[0]}" : "")}, " +
              $"{(risk == "Low" ? "playing it safe" : risk == "High" ? "swinging big" : "balancing risk")} " +
              $"with {style.ToLowerInvariant()} instincts."
            : "A World Cup fan ready for the tournament.";

        return new UserProfile
        {
            FavoriteTeam = team,
            FavoritePlayers = players,
            Location = location,
            BarVibe = vibe,
            BettingStyle = style,
            RiskTolerance = risk,
            Summary = summary,
        };
    }
}
