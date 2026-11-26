using MudBlazor;

namespace UdapEd.Shared.Components;

/// <summary>
/// UdapEd visual theme: ink navy neutrals with a single steel-teal accent taken from the
/// trust-anchor node of the logo. IBM Plex Sans for the interface, IBM Plex Mono for
/// tokens, certificates and JSON (see --udaped-font-mono in app.css).
/// </summary>
public static class UdapEdTheme
{
    public const string Ink = "#152A38";
    public const string Accent = "#1F7A8C";
    public const string AccentOnDark = "#5CB8CC";

    private static readonly string[] SansStack =
        ["IBM Plex Sans", "Segoe UI", "Helvetica Neue", "Arial", "sans-serif"];

    public static MudTheme Create()
    {
        var typography = new Typography();
        typography.Default.FontFamily = SansStack;
        typography.Default.LetterSpacing = "0";
        typography.H1.FontFamily = SansStack;
        typography.H2.FontFamily = SansStack;
        typography.H3.FontFamily = SansStack;
        typography.H4.FontFamily = SansStack;
        typography.H5.FontFamily = SansStack;
        typography.H6.FontFamily = SansStack;
        typography.H4.LetterSpacing = "-0.01em";
        typography.H5.LetterSpacing = "-0.005em";
        typography.H6.LetterSpacing = "0";
        typography.Button.FontFamily = SansStack;
        typography.Button.TextTransform = "none";
        typography.Button.LetterSpacing = "0.01em";
        typography.Subtitle1.FontFamily = SansStack;
        typography.Subtitle2.FontFamily = SansStack;
        typography.Body1.FontFamily = SansStack;
        typography.Body2.FontFamily = SansStack;
        typography.Caption.FontFamily = SansStack;
        typography.Overline.FontFamily = SansStack;
        typography.Overline.TextTransform = "none";
        typography.Overline.LetterSpacing = "0.02em";

        return new MudTheme
        {
            Typography = typography,
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "6px",
                AppbarHeight = "60px",
                DrawerWidthLeft = "248px"
            },
            PaletteLight = new PaletteLight
            {
                Primary = Accent,
                PrimaryContrastText = "#FFFFFF",
                Secondary = "#4E6274",
                SecondaryContrastText = "#FFFFFF",
                Tertiary = "#8A6A2E",
                Info = "#2F6FA8",
                Success = "#2E7D4F",
                Warning = "#B26A00",
                Error = "#B3261E",
                Dark = Ink,
                Black = "#0E1A23",
                TextPrimary = "#1B2731",
                TextSecondary = "#55636F",
                TextDisabled = "rgba(27,39,49,0.38)",
                ActionDefault = "#55636F",
                Background = "#F4F6F8",
                BackgroundGray = "#EBEFF2",
                Surface = "#FFFFFF",
                AppbarBackground = "#FFFFFF",
                AppbarText = "#1B2731",
                DrawerBackground = "#FFFFFF",
                DrawerText = "#1B2731",
                DrawerIcon = "#55636F",
                LinesDefault = "#D9E0E6",
                LinesInputs = "#B9C3CC",
                TableLines = "#E3E8EC",
                Divider = "#E1E6EB",
                DividerLight = "#EEF1F4",
                OverlayLight = "rgba(244,246,248,0.6)"
            },
            PaletteDark = new PaletteDark
            {
                Primary = AccentOnDark,
                PrimaryContrastText = "#0E1A23",
                Secondary = "#9AB0C2",
                SecondaryContrastText = "#0E1A23",
                Tertiary = "#D2B06A",
                Info = "#6FA8DC",
                Success = "#5FBF86",
                Warning = "#E0A040",
                Error = "#F07A70",
                Dark = "#C9D4DD",
                Black = "#0B1117",
                TextPrimary = "#DCE4EA",
                TextSecondary = "#9AA8B4",
                TextDisabled = "rgba(220,228,234,0.38)",
                ActionDefault = "#9AA8B4",
                Background = "#0F161D",
                BackgroundGray = "#131C25",
                Surface = "#16202A",
                AppbarBackground = "#121B23",
                AppbarText = "#DCE4EA",
                DrawerBackground = "#121B23",
                DrawerText = "#DCE4EA",
                DrawerIcon = "#9AA8B4",
                LinesDefault = "#2A3642",
                LinesInputs = "#3A4855",
                TableLines = "#26313C",
                Divider = "#26313C",
                DividerLight = "#1E2832"
            }
        };
    }
}
