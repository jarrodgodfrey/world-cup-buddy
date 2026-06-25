namespace WorldCupBuddy.Models;

public record TeamInfo(string Name, int Rating, string Group, string Flag);

/// <summary>
/// The 32-nation field used across the app: Elo-style ratings (loosely based on
/// 2026 FIFA rankings), group assignments (8 groups A–H, traditional format),
/// and flag emojis. Shared by OddsService (flags) and SimulationService (ratings).
/// </summary>
public static class WorldCupData
{
    public static readonly IReadOnlyList<TeamInfo> Teams = new List<TeamInfo>
    {
        // Group A
        new("France",        2020, "A", "🇫🇷"),
        new("Belgium",       1770, "A", "🇧🇪"),
        new("Morocco",       1625, "A", "🇲🇦"),
        new("Canada",        1500, "A", "🇨🇦"),
        // Group B
        new("Brazil",        2005, "B", "🇧🇷"),
        new("Croatia",       1725, "B", "🇭🇷"),
        new("Japan",         1610, "B", "🇯🇵"),
        new("Iran",          1495, "B", "🇮🇷"),
        // Group C
        new("Argentina",     1990, "C", "🇦🇷"),
        new("Uruguay",       1705, "C", "🇺🇾"),
        new("Senegal",       1600, "C", "🇸🇳"),
        new("Poland",        1485, "C", "🇵🇱"),
        // Group D
        new("England",       1975, "D", "🏴󠁧󠁢󠁥󠁮󠁧󠁿"),
        new("Colombia",      1690, "D", "🇨🇴"),
        new("Serbia",        1585, "D", "🇷🇸"),
        new("Nigeria",       1470, "D", "🇳🇬"),
        // Group E
        new("Spain",         1965, "E", "🇪🇸"),
        new("USA",           1680, "E", "🇺🇸"),
        new("South Korea",   1560, "E", "🇰🇷"),
        new("Cameroon",      1455, "E", "🇨🇲"),
        // Group F
        new("Portugal",      1945, "F", "🇵🇹"),
        new("Mexico",        1670, "F", "🇲🇽"),
        new("Ecuador",       1540, "F", "🇪🇨"),
        new("Ghana",         1445, "F", "🇬🇭"),
        // Group G
        new("Germany",       1905, "G", "🇩🇪"),
        new("Switzerland",   1660, "G", "🇨🇭"),
        new("Peru",          1520, "G", "🇵🇪"),
        new("Saudi Arabia",  1425, "G", "🇸🇦"),
        // Group H
        new("Netherlands",   1790, "H", "🇳🇱"),
        new("Denmark",       1650, "H", "🇩🇰"),
        new("Australia",     1510, "H", "🇦🇺"),
        new("Qatar",         1400, "H", "🇶🇦"),
    };

    private static readonly Dictionary<string, TeamInfo> ByName =
        Teams.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    public static readonly string[] GroupNames = { "A", "B", "C", "D", "E", "F", "G", "H" };

    /// <summary>
    /// Flags for nations that may appear in the live Odds API feed but aren't in
    /// the 32-team simulator field (qualifiers, friendlies, expanded brackets).
    /// </summary>
    private static readonly Dictionary<string, string> ExtraFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Algeria"] = "🇩🇿",
        ["Austria"] = "🇦🇹",
        ["Bosnia & Herzegovina"] = "🇧🇦",
        ["Bosnia and Herzegovina"] = "🇧🇦",
        ["Cape Verde"] = "🇨🇻",
        ["Curaçao"] = "🇨🇼",
        ["Curacao"] = "🇨🇼",
        ["DR Congo"] = "🇨🇩",
        ["Egypt"] = "🇪🇬",
        ["Iraq"] = "🇮🇶",
        ["Ivory Coast"] = "🇨🇮",
        ["Jordan"] = "🇯🇴",
        ["New Zealand"] = "🇳🇿",
        ["Norway"] = "🇳🇴",
        ["Panama"] = "🇵🇦",
        ["Paraguay"] = "🇵🇾",
        ["South Africa"] = "🇿🇦",
        ["Sweden"] = "🇸🇪",
        ["Tunisia"] = "🇹🇳",
        ["Turkey"] = "🇹🇷",
        ["Türkiye"] = "🇹🇷",
        ["Uzbekistan"] = "🇺🇿",
    };

    public static string Flag(string team)
    {
        if (ByName.TryGetValue(team, out var t)) return t.Flag;
        if (ExtraFlags.TryGetValue(team, out var f)) return f;
        return "🏳️";
    }

    public static TeamInfo? Find(string team) =>
        ByName.TryGetValue(team, out var t) ? t : null;
}
