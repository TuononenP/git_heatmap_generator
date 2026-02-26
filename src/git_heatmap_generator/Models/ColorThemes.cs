using SixLabors.ImageSharp;

namespace git_heatmap_generator.Models;

public enum ColorTheme
{
    Default,
    Blue,
    Red,
    Purple
}

public enum ColorMode
{
    Dark,
    Light
}

public record ColorScheme
{
    public Color BackgroundColor { get; init; }
    public Color CellEmptyColor { get; init; }
    public Color Level1Color { get; init; }
    public Color Level2Color { get; init; }
    public Color Level3Color { get; init; }
    public Color Level4Color { get; init; }
    public Color TextColor { get; init; }
    public Color SubtextColor { get; init; }

    public Color GetColorForCount(int count)
    {
        if (count == 0) return CellEmptyColor;
        if (count <= 3) return Level1Color;
        if (count <= 6) return Level2Color;
        if (count <= 9) return Level3Color;
        return Level4Color;
    }

    public static ColorScheme GetTheme(ColorTheme theme, ColorMode mode = ColorMode.Dark)
    {
        bool isLight = mode == ColorMode.Light;
        
        var scheme = new ColorScheme
        {
            BackgroundColor = isLight ? Color.White : Color.ParseHex("#0d1117"),
            CellEmptyColor = isLight ? Color.ParseHex("#ebedf0") : Color.ParseHex("#161b22"),
            TextColor = isLight ? Color.ParseHex("#24292f") : Color.White,
            SubtextColor = isLight ? Color.ParseHex("#57606a") : Color.ParseHex("#7d8590")
        };

        return theme switch
        {
            ColorTheme.Blue => scheme with
            {
                Level1Color = isLight ? Color.ParseHex("#ddf4ff") : Color.ParseHex("#032d63"),
                Level2Color = isLight ? Color.ParseHex("#54aeff") : Color.ParseHex("#0550ae"),
                Level3Color = isLight ? Color.ParseHex("#0969da") : Color.ParseHex("#0969da"),
                Level4Color = isLight ? Color.ParseHex("#032d63") : Color.ParseHex("#54aeff")
            },
            ColorTheme.Red => scheme with
            {
                Level1Color = isLight ? Color.ParseHex("#ffebe9") : Color.ParseHex("#67060c"),
                Level2Color = isLight ? Color.ParseHex("#fa7970") : Color.ParseHex("#9e1523"),
                Level3Color = isLight ? Color.ParseHex("#da3633") : Color.ParseHex("#da3633"),
                Level4Color = isLight ? Color.ParseHex("#67060c") : Color.ParseHex("#fa7970")
            },
            ColorTheme.Purple => scheme with
            {
                Level1Color = isLight ? Color.ParseHex("#f5f0ff") : Color.ParseHex("#3c1762"),
                Level2Color = isLight ? Color.ParseHex("#a371f7") : Color.ParseHex("#5c2b97"),
                Level3Color = isLight ? Color.ParseHex("#8250df") : Color.ParseHex("#8250df"),
                Level4Color = isLight ? Color.ParseHex("#3c1762") : Color.ParseHex("#a371f7")
            },
            _ => scheme with // Default (GitHub-style)
            {
                Level1Color = isLight ? Color.ParseHex("#9be9a8") : Color.ParseHex("#0e4429"),
                Level2Color = isLight ? Color.ParseHex("#40c463") : Color.ParseHex("#006d32"),
                Level3Color = isLight ? Color.ParseHex("#30a14e") : Color.ParseHex("#26a641"),
                Level4Color = isLight ? Color.ParseHex("#216e39") : Color.ParseHex("#39d353")
            }
        };
    }
}
