using MudBlazor;

namespace WorldCupBuddy;

/// <summary>
/// MudBlazor theme mapped to the World Cup Buddy palette so Mud components
/// (tables, charts, chips, etc.) match the custom dark sports aesthetic.
/// </summary>
public static class WcbTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#e94560",          // electric red
            Secondary = "#f5a623",        // golden yellow
            Tertiary = "#0f3460",         // rich blue
            Background = "#1a1a2e",       // deep navy
            BackgroundGray = "#16213e",
            Surface = "#0f3460",          // card background
            AppbarBackground = "#16213e",
            AppbarText = "#ffffff",
            DrawerBackground = "#16213e",
            DrawerText = "#e0e0e0",
            TextPrimary = "#ffffff",
            TextSecondary = "#e0e0e0",
            ActionDefault = "#e0e0e0",
            Success = "#00c853",
            Info = "#f5a623",
            LinesDefault = "rgba(255,255,255,0.1)",
            LinesInputs = "rgba(255,255,255,0.2)",
            TableLines = "rgba(255,255,255,0.1)",
            TableStriped = "rgba(255,255,255,0.02)",
            TableHover = "rgba(245,166,35,0.08)",
            OverlayDark = "rgba(10,16,33,0.7)",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "system-ui", "-apple-system", "sans-serif" }
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px"
        }
    };

    /// <summary>Accent palette reused for MudBlazor charts.</summary>
    public static readonly string[] ChartPalette =
    {
        "#e94560", "#f5a623", "#00c853", "#4ea8de", "#b15bff",
        "#ff8f5e", "#2ec4b6", "#ffd23f", "#ff5d8f", "#7bdff2",
    };
}
