using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCupBuddy.Models;

namespace WorldCupBuddy.Services;

/// <summary>
/// Fetches World Cup 2026 odds from The Odds API, compares the sharp Pinnacle
/// line against the major US public books, and computes expected value per bet.
/// Always degrades gracefully to a realistic mock dataset.
/// </summary>
public class OddsService
{
    // The Odds API's active FIFA World Cup key (the "_2026" variant returns
    // "Unknown sport"). This is the live tournament feed.
    private const string SportKey = "soccer_fifa_world_cup";
    private static readonly string[] BookKeys = { "pinnacle", "draftkings", "fanduel", "betmgm" };

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OddsService> _logger;

    public OddsService(HttpClient http, IConfiguration config, ILogger<OddsService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    /// <summary>True when results came from the mock dataset rather than the live API.</summary>
    public bool UsingMockData { get; private set; }

    /// <summary>
    /// Returns one <see cref="EVBet"/> per match, sorted by best available EV descending.
    /// </summary>
    public async Task<List<EVBet>> GetEdgesAsync(CancellationToken ct = default)
    {
        var matches = await FetchMatchesAsync(ct);
        var edges = matches.Select(BuildEdge)
            // Hide matches with an implausibly high best EV (stale/erroneous longshot lines).
            .Where(e => e.BestEv <= OddsThresholds.MaxEv)
            .ToList();

        // Sort matches by their strongest available edge.
        edges.Sort((a, b) => b.BestEv.CompareTo(a.BestEv));
        return edges;
    }

    // ---- API access -------------------------------------------------------

    private async Task<List<Match>> FetchMatchesAsync(CancellationToken ct)
    {
        var apiKey = _config["OddsApi:ApiKey"];
        var baseUrl = _config["OddsApi:BaseUrl"] ?? "https://api.the-odds-api.com/v4";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_API_KEY_HERE")
        {
            _logger.LogInformation("Odds API key not configured — using mock dataset.");
            UsingMockData = true;
            return MockMatches();
        }

        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/sports/{SportKey}/odds" +
                      $"?apiKey={apiKey}" +
                      "&regions=us" +
                      "&markets=h2h,totals" +
                      "&oddsFormat=american" +
                      $"&bookmakers={string.Join(',', BookKeys)}";

            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var dto = await JsonSerializer.DeserializeAsync<List<ApiEvent>>(stream, JsonOpts, ct);

            var matches = (dto ?? new()).Select(MapEvent).ToList();
            if (matches.Count == 0)
            {
                _logger.LogWarning("Odds API returned no events — falling back to mock dataset.");
                UsingMockData = true;
                return MockMatches();
            }

            UsingMockData = false;
            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Odds API request failed — falling back to mock dataset.");
            UsingMockData = true;
            return MockMatches();
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static Match MapEvent(ApiEvent e) => new()
    {
        Id = e.Id ?? Guid.NewGuid().ToString(),
        HomeTeam = e.HomeTeam ?? "",
        AwayTeam = e.AwayTeam ?? "",
        CommenceTime = e.CommenceTime,
        Bookmakers = (e.Bookmakers ?? new()).Select(b => new BookmakerOdds
        {
            Key = b.Key ?? "",
            Title = b.Title ?? "",
            Markets = (b.Markets ?? new()).Select(m => new MarketOdds
            {
                Key = m.Key ?? "",
                Outcomes = (m.Outcomes ?? new()).Select(o => new OutcomeOdds
                {
                    Name = o.Name ?? "",
                    Price = o.Price,
                    Point = o.Point,
                    Link = o.Link
                }).ToList()
            }).ToList()
        }).ToList()
    };

    // ---- Edge / EV computation -------------------------------------------

    private static EVBet BuildEdge(Match m)
    {
        var rows = new List<OddsComparison>();
        rows.AddRange(BuildH2hRows(m));
        rows.AddRange(BuildTotalsRows(m));

        var bet = new EVBet
        {
            MatchId = m.Id,
            HomeTeam = m.HomeTeam,
            AwayTeam = m.AwayTeam,
            HomeFlag = WorldCupData.Flag(m.HomeTeam),
            AwayFlag = WorldCupData.Flag(m.AwayTeam),
            CommenceTime = m.CommenceTime,
            Rows = rows
        };

        bet.BestEv = rows.Count == 0 ? 0 : rows.Max(r => r.BestEv);
        if (bet.BestEv == double.NegativeInfinity) bet.BestEv = 0;

        foreach (var r in rows)
        {
            foreach (var ev in new[] { r.DraftKingsEv, r.FanDuelEv, r.BetMgmEv })
            {
                if (!ev.HasValue) continue;
                if (ev.Value > OddsThresholds.StrongValue) bet.StrongValueBetCount++;
                else if (ev.Value > OddsThresholds.Value) bet.ValueBetCount++;
            }
        }

        bet.EdgeScore = bet.StrongValueBetCount > 0 ? EdgeScore.StrongValue
                      : bet.ValueBetCount > 0 ? EdgeScore.Value
                      : EdgeScore.None;
        return bet;
    }

    private static IEnumerable<OddsComparison> BuildH2hRows(Match m)
    {
        var pin = m.Book("pinnacle")?.Market("h2h");
        if (pin is null) yield break;

        // Vig-removed Pinnacle probabilities across the three outcomes.
        var trueProbs = NormalizeVig(pin.Outcomes.Select(o => ImpliedProb(o.Price)).ToArray());
        var probByName = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < pin.Outcomes.Count; i++)
            probByName[pin.Outcomes[i].Name] = trueProbs[i];

        (string label, string outcome)[] selections =
        {
            ($"{m.HomeTeam} Win", m.HomeTeam),
            ("Draw", "Draw"),
            ($"{m.AwayTeam} Win", m.AwayTeam),
        };

        foreach (var (label, outcome) in selections)
        {
            var pinPrice = pin.Outcomes.FirstOrDefault(o =>
                o.Name.Equals(outcome, StringComparison.OrdinalIgnoreCase))?.Price;
            if (pinPrice is null) continue;

            probByName.TryGetValue(outcome, out var trueProb);

            yield return new OddsComparison
            {
                Selection = label,
                Market = "h2h",
                PinnacleAmerican = pinPrice,
                PinnacleTrueProb = trueProb,
                DraftKingsAmerican = PublicPrice(m, "draftkings", "h2h", outcome),
                FanDuelAmerican = PublicPrice(m, "fanduel", "h2h", outcome),
                BetMgmAmerican = PublicPrice(m, "betmgm", "h2h", outcome),
                DraftKingsLink = PublicLink(m, "draftkings", "h2h", outcome),
                FanDuelLink = PublicLink(m, "fanduel", "h2h", outcome),
                BetMgmLink = PublicLink(m, "betmgm", "h2h", outcome),
            }.WithEv();
        }
    }

    private static IEnumerable<OddsComparison> BuildTotalsRows(Match m)
    {
        var pin = m.Book("pinnacle")?.Market("totals");
        if (pin is null) yield break;

        // Prefer the 2.5 line if present, otherwise the first available total.
        var point = pin.Outcomes.FirstOrDefault(o => o.Point == 2.5)?.Point
                    ?? pin.Outcomes.FirstOrDefault()?.Point
                    ?? 2.5;

        var legs = pin.Outcomes.Where(o => o.Point == point).ToList();
        if (legs.Count < 2) yield break;

        var trueProbs = NormalizeVig(legs.Select(o => ImpliedProb(o.Price)).ToArray());
        var probByName = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < legs.Count; i++)
            probByName[legs[i].Name] = trueProbs[i];

        foreach (var side in new[] { "Over", "Under" })
        {
            var pinPrice = legs.FirstOrDefault(o =>
                o.Name.Equals(side, StringComparison.OrdinalIgnoreCase))?.Price;
            if (pinPrice is null) continue;

            probByName.TryGetValue(side, out var trueProb);

            yield return new OddsComparison
            {
                Selection = $"{side} {point:0.0}",
                Market = "totals",
                PinnacleAmerican = pinPrice,
                PinnacleTrueProb = trueProb,
                DraftKingsAmerican = PublicTotalsPrice(m, "draftkings", side, point),
                FanDuelAmerican = PublicTotalsPrice(m, "fanduel", side, point),
                BetMgmAmerican = PublicTotalsPrice(m, "betmgm", side, point),
                DraftKingsLink = PublicTotalsLink(m, "draftkings", side, point),
                FanDuelLink = PublicTotalsLink(m, "fanduel", side, point),
                BetMgmLink = PublicTotalsLink(m, "betmgm", side, point),
            }.WithEv();
        }
    }

    private static double? PublicPrice(Match m, string book, string market, string outcome) =>
        m.Book(book)?.Market(market)?.Outcomes
            .FirstOrDefault(o => o.Name.Equals(outcome, StringComparison.OrdinalIgnoreCase))?.Price;

    private static string? PublicLink(Match m, string book, string market, string outcome) =>
        m.Book(book)?.Market(market)?.Outcomes
            .FirstOrDefault(o => o.Name.Equals(outcome, StringComparison.OrdinalIgnoreCase))?.Link;

    private static string? PublicTotalsLink(Match m, string book, string side, double point) =>
        m.Book(book)?.Market("totals")?.Outcomes
            .FirstOrDefault(o => o.Name.Equals(side, StringComparison.OrdinalIgnoreCase) && o.Point == point)?.Link;

    private static double? PublicTotalsPrice(Match m, string book, string side, double point) =>
        m.Book(book)?.Market("totals")?.Outcomes
            .FirstOrDefault(o => o.Name.Equals(side, StringComparison.OrdinalIgnoreCase) && o.Point == point)?.Price;

    // ---- Odds math --------------------------------------------------------

    public static double ImpliedProb(double american) =>
        american > 0 ? 100.0 / (american + 100.0) : Math.Abs(american) / (Math.Abs(american) + 100.0);

    public static double ToDecimal(double american) =>
        american > 0 ? (american / 100.0) + 1.0 : (100.0 / Math.Abs(american)) + 1.0;

    private static double[] NormalizeVig(double[] probs)
    {
        var sum = probs.Sum();
        if (sum <= 0) return probs;
        return probs.Select(p => p / sum).ToArray();
    }

    // ---- Mock dataset -----------------------------------------------------

    private static List<Match> MockMatches()
    {
        var baseDate = new DateTime(2026, 6, 12, 18, 0, 0, DateTimeKind.Utc);

        return new List<Match>
        {
            // Heavy favorite — value sits on the long shots at a couple of books.
            MockMatch("m1", "Argentina", "Canada", baseDate.AddHours(0),
                h2h: (-360, 470, 950), totals25: (-130, 105),
                dk: (-330, 520, 1100, -120, 110),
                fd: (-340, 480, 1000, -125, 100),
                mgm: (-350, 460, 980, -118, 102)),

            MockMatch("m2", "Brazil", "Iran", baseDate.AddHours(3),
                h2h: (-420, 520, 1050), totals25: (-145, 120),
                dk: (-400, 560, 1200, -135, 118),
                fd: (-410, 540, 1150, -140, 122),
                mgm: (-405, 500, 1080, -138, 115)),

            MockMatch("m3", "England", "Serbia", baseDate.AddHours(6),
                h2h: (-185, 310, 520), totals25: (-118, -102),
                dk: (-170, 330, 560, -110, -108),
                fd: (-180, 300, 540, -115, -100),
                mgm: (-175, 320, 510, -112, -104)),

            // Near coin-flip — edges spread across all three outcomes.
            MockMatch("m4", "USA", "Mexico", baseDate.AddDays(1),
                h2h: (135, 215, 205), totals25: (-110, -110),
                dk: (150, 230, 220, -105, -114),
                fd: (140, 225, 215, -108, -110),
                mgm: (145, 210, 210, -112, -106)),

            MockMatch("m5", "Germany", "Switzerland", baseDate.AddDays(1).AddHours(3),
                h2h: (-160, 280, 430), totals25: (-125, 105),
                dk: (-150, 300, 470, -118, 112),
                fd: (-155, 290, 450, -122, 108),
                mgm: (-158, 270, 440, -120, 104)),

            MockMatch("m6", "Spain", "South Korea", baseDate.AddDays(1).AddHours(6),
                h2h: (-240, 350, 620), totals25: (-135, 112),
                dk: (-225, 380, 680, -125, 120),
                fd: (-235, 360, 650, -130, 116),
                mgm: (-230, 340, 640, -128, 110)),

            MockMatch("m7", "Portugal", "Ecuador", baseDate.AddDays(2),
                h2h: (-210, 330, 560), totals25: (-120, 100),
                dk: (-195, 360, 600, -112, 108),
                fd: (-205, 340, 580, -118, 104),
                mgm: (-200, 320, 570, -115, 102)),

            MockMatch("m8", "Netherlands", "Australia", baseDate.AddDays(2).AddHours(3),
                h2h: (-260, 360, 680), totals25: (-128, 106),
                dk: (-245, 390, 740, -120, 114),
                fd: (-255, 370, 710, -124, 110),
                mgm: (-250, 350, 700, -122, 108)),
        };
    }

    // h2h: (home, draw, away) American. totals25: (over, under) Pinnacle.
    // Each public book: (home, draw, away, over, under).
    private static Match MockMatch(
        string id, string home, string away, DateTime when,
        (double home, double draw, double away) h2h,
        (double over, double under) totals25,
        (double home, double draw, double away, double over, double under) dk,
        (double home, double draw, double away, double over, double under) fd,
        (double home, double draw, double away, double over, double under) mgm)
    {
        BookmakerOdds Book(string key, string title,
            double h, double d, double a, double ov, double un) => new()
        {
            Key = key,
            Title = title,
            Markets = new()
            {
                new MarketOdds
                {
                    Key = "h2h",
                    Outcomes = new()
                    {
                        new() { Name = home, Price = h },
                        new() { Name = "Draw", Price = d },
                        new() { Name = away, Price = a },
                    }
                },
                new MarketOdds
                {
                    Key = "totals",
                    Outcomes = new()
                    {
                        new() { Name = "Over", Price = ov, Point = 2.5 },
                        new() { Name = "Under", Price = un, Point = 2.5 },
                    }
                }
            }
        };

        return new Match
        {
            Id = id,
            HomeTeam = home,
            AwayTeam = away,
            CommenceTime = when,
            Bookmakers = new()
            {
                Book("pinnacle", "Pinnacle", h2h.home, h2h.draw, h2h.away, totals25.over, totals25.under),
                Book("draftkings", "DraftKings", dk.home, dk.draw, dk.away, dk.over, dk.under),
                Book("fanduel", "FanDuel", fd.home, fd.draw, fd.away, fd.over, fd.under),
                Book("betmgm", "BetMGM", mgm.home, mgm.draw, mgm.away, mgm.over, mgm.under),
            }
        };
    }

    // ---- API DTOs ---------------------------------------------------------

    private class ApiEvent
    {
        public string? Id { get; set; }
        [JsonPropertyName("commence_time")] public DateTime CommenceTime { get; set; }
        [JsonPropertyName("home_team")] public string? HomeTeam { get; set; }
        [JsonPropertyName("away_team")] public string? AwayTeam { get; set; }
        public List<ApiBookmaker>? Bookmakers { get; set; }
    }

    private class ApiBookmaker
    {
        public string? Key { get; set; }
        public string? Title { get; set; }
        public List<ApiMarket>? Markets { get; set; }
    }

    private class ApiMarket
    {
        public string? Key { get; set; }
        public List<ApiOutcome>? Outcomes { get; set; }
    }

    private class ApiOutcome
    {
        public string? Name { get; set; }
        public double Price { get; set; }
        public double? Point { get; set; }
        public string? Link { get; set; }
    }
}

internal static class OddsComparisonExtensions
{
    /// <summary>Fills in the per-book EV from the Pinnacle true probability.</summary>
    public static OddsComparison WithEv(this OddsComparison c)
    {
        double? Ev(double? american) =>
            american.HasValue ? c.PinnacleTrueProb * OddsService.ToDecimal(american.Value) - 1.0 : null;

        c.DraftKingsEv = Ev(c.DraftKingsAmerican);
        c.FanDuelEv = Ev(c.FanDuelAmerican);
        c.BetMgmEv = Ev(c.BetMgmAmerican);
        return c;
    }
}
