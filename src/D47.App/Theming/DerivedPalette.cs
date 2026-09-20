using Avalonia.Media;
using D47.Core.Interface;

namespace D47.App.Theming;

/// <summary>
/// Every role mixed or hue-swapped from a <see cref="Palette"/>'s Accent and Background, computed once
/// so <see cref="ThemeManager.Apply"/> and a fallback swatch agree (#329).
/// </summary>
public sealed record DerivedPalette
{
    public required Color Surface { get; init; }

    public required Color SurfaceAlt { get; init; }

    public required Color Border { get; init; }

    public required Color Text { get; init; }

    public required Color TextMuted { get; init; }

    public required Color TextFaint { get; init; }

    public required Color Rule { get; init; }

    public required Color FillLow { get; init; }

    public required Color FillHigh { get; init; }

    public required Color FillHigher { get; init; }

    public required Color AccentBorder { get; init; }

    public required Color AccentInk { get; init; }

    public required Color Danger { get; init; }

    public required Color Warn { get; init; }

    public required Color Good { get; init; }

    public required Color Info { get; init; }

    public required Color InfoFill { get; init; }

    public required Color InfoBorder { get; init; }

    public required Color InfoInk { get; init; }

    public required Color CardFill { get; init; }

    public required Color CardFillSelected { get; init; }

    public required Color RowFill { get; init; }

    public required Color TagBorder { get; init; }

    public required Color TabStripRule { get; init; }

    public required Color PaneBorder { get; init; }

    public required Color TagInk { get; init; }

    public static DerivedPalette From(Palette palette)
    {
        var background = palette.Background;
        var accent = palette.Accent;

        Color Onto(double t) => Mix(background, accent, t);

        var text = palette.InkIsAccent ? accent : palette.Text;
        var info = Hue(accent, 248);

        return new DerivedPalette
        {
            Surface = Onto(0.09),
            SurfaceAlt = Onto(0.16),
            Border = Onto(0.24),
            Text = text,
            TextMuted = Onto(0.66),
            TextFaint = Onto(0.42),
            Rule = Onto(0.50),
            FillLow = Onto(0.09),
            FillHigh = Onto(0.16),
            FillHigher = Onto(0.27),
            AccentBorder = Onto(0.35),
            AccentInk = palette.IsDark
                ? Mix(Colors.White, accent, 0.58)
                : Mix(Color.Parse("#140800"), accent, 0.55),
            Danger = Hue(accent, 27),
            Warn = Hue(accent, 82),
            Good = Hue(accent, 146),
            Info = info,
            InfoFill = Mix(background, info, 0.09),
            InfoBorder = Mix(background, info, 0.35),
            InfoInk = Mix(text, info, 0.35),
            CardFill = Onto(0.05),
            CardFillSelected = Onto(0.14),
            RowFill = Onto(0.05),
            TagBorder = Onto(0.60),
            TabStripRule = Onto(0.70),
            PaneBorder = Onto(0.30),
            TagInk = Onto(0.85),
        };
    }

    /// <summary>Mixes <paramref name="to"/> into <paramref name="from"/> by <paramref name="t"/>, in OKLab.</summary>
    private static Color Mix(Color from, Color to, double t)
    {
        var (r, g, b) = OklabMixing.Mix((from.R, from.G, from.B), (to.R, to.G, to.B), t);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>Keeps <paramref name="colour"/>'s lightness and chroma, substituting its hue.</summary>
    private static Color Hue(Color colour, double degrees)
    {
        var (r, g, b) = OklabMixing.WithHue((colour.R, colour.G, colour.B), degrees);
        return Color.FromRgb(r, g, b);
    }
}
