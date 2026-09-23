using Avalonia.Media;
using D47.Core.Interface;

namespace D47.App.Theming;

/// <summary>Elite's token table for one theme: fixed neutrals, one meaning per coloured token.</summary>
public sealed record Palette
{
    public required bool IsDark { get; init; }

    /// <summary>Whether the theme draws bloom and scanlines.</summary>
    public required bool Glows { get; init; }

    /// <summary>Window ground.</summary>
    public required Color Bg { get; init; }

    /// <summary>Title bar, modal ground.</summary>
    public required Color Bar { get; init; }

    /// <summary>Read-only data tile.</summary>
    public required Color Slab { get; init; }

    /// <summary>Identity and speech.</summary>
    public required Color White { get; init; }

    /// <summary>Labels, helper prose.</summary>
    public required Color Grey { get; init; }

    /// <summary>Placeholders, disabled.</summary>
    public required Color Grey2 { get; init; }

    /// <summary>Values, interactive text, rules, frames.</summary>
    public required Color A { get; init; }

    /// <summary>Text on solid <see cref="A"/>.</summary>
    public required Color Knock { get; init; }

    /// <summary>Secondary text in a selected row.</summary>
    public required Color Brown { get; init; }

    /// <summary>Yours, here, ready.</summary>
    public required Color Cyan { get; init; }

    /// <summary>Confirmed, met.</summary>
    public required Color Blue { get; init; }

    /// <summary>Destructive, hostile, locked, error.</summary>
    public required Color Red { get; init; }

    /// <summary>Stored, capacity.</summary>
    public required Color Yellow { get; init; }

    /// <summary><see cref="A"/> at 20% onto <see cref="Bg"/>, in OKLab.</summary>
    public Color Tile => Mix(Bg, A, 0.20);

    /// <summary><see cref="A"/> at 30% onto <see cref="Bg"/>, in OKLab.</summary>
    public Color Tile2 => Mix(Bg, A, 0.30);

    /// <summary><see cref="A"/> at 55% onto <see cref="Bg"/>, in OKLab.</summary>
    public Color Line => Mix(Bg, A, 0.55);

    /// <summary><see cref="A"/> at 28% onto <see cref="Bg"/>, in OKLab.</summary>
    public Color Line2 => Mix(Bg, A, 0.28);

    /// <summary><see cref="Cyan"/> at 7% onto <see cref="Bg"/>, in OKLab: the ground of the Commander's own turns.</summary>
    public Color CyanGround => Mix(Bg, Cyan, 0.07);

    /// <summary>Passes the coloured tokens through Elite's HUD matrix, leaving the neutrals as they are.</summary>
    public Palette RecolouredBy(GuiColourMatrix matrix) => this with
    {
        A = Transform(matrix, A),
        Knock = Transform(matrix, Knock),
        Brown = Transform(matrix, Brown),
        Cyan = Transform(matrix, Cyan),
        Blue = Transform(matrix, Blue),
        Red = Transform(matrix, Red),
        Yellow = Transform(matrix, Yellow),
    };

    /// <summary>Mixes <paramref name="to"/> into <paramref name="from"/> by <paramref name="t"/>, in OKLab.</summary>
    public static Color Mix(Color from, Color to, double t)
    {
        var (r, g, b) = OklabMixing.Mix((from.R, from.G, from.B), (to.R, to.G, to.B), t);
        return Color.FromRgb(r, g, b);
    }

    private static Color Transform(GuiColourMatrix matrix, Color colour)
    {
        var (r, g, b) = matrix.Transform(colour.R, colour.G, colour.B);
        return Color.FromArgb(colour.A, r, g, b);
    }
}

/// <summary>The shipped palettes, one per <see cref="ThemeCatalog"/> id.</summary>
public static class Palettes
{
    public static Palette Elite { get; } = new()
    {
        IsDark = true,
        Glows = true,
        Bg = Color.Parse("#070606"),
        Bar = Color.Parse("#0F0D0C"),
        Slab = Color.Parse("#232120"),
        White = Color.Parse("#EDE9E3"),
        Grey = Color.Parse("#A09B94"),
        Grey2 = Color.Parse("#6E6A65"),
        A = Color.Parse("#FF7A1A"),
        Knock = Color.Parse("#140800"),
        Brown = Color.Parse("#6B2F00"),
        Cyan = Color.Parse("#33D6E8"),
        Blue = Color.Parse("#1FA8F5"),
        Red = Color.Parse("#F0343F"),
        Yellow = Color.Parse("#F5D426"),
    };

    public static Palette Dark { get; } = Mixed(
        isDark: true,
        bg: Color.Parse("#1E1E1E"),
        bar: Color.Parse("#2D2D2D"),
        slab: Color.Parse("#252526"),
        white: Color.Parse("#D4D4D4"),
        grey: Color.Parse("#9D9D9D"),
        a: Color.Parse("#3794FF"),
        knock: Color.Parse("#0B1F33"),
        cyan: Color.Parse("#4EC9B0"),
        blue: Color.Parse("#569CD6"),
        red: Color.Parse("#F48771"),
        yellow: Color.Parse("#DCDCAA"));

    public static Palette Light { get; } = Mixed(
        isDark: false,
        bg: Color.Parse("#F4F1EB"),
        bar: Color.Parse("#E6E1D8"),
        slab: Color.Parse("#E4DFD6"),
        white: Color.Parse("#1C1917"),
        grey: Color.Parse("#5E5852"),
        a: Color.Parse("#B84E00"),
        knock: Color.Parse("#FFFFFF"),
        cyan: Color.Parse("#007C8A"),
        blue: Color.Parse("#0B6BCB"),
        red: Color.Parse("#C8192B"),
        yellow: Color.Parse("#8A6D00"));

    /// <summary>The palette for a theme id, before any HUD matrix is applied.</summary>
    public static Palette For(string? themeId) => ThemeCatalog.Selected(themeId).Id switch
    {
        ThemeCatalog.Dark => Dark,
        ThemeCatalog.Light => Light,
        _ => Elite,
    };

    /// <summary>A palette without glow whose <see cref="Palette.Grey2"/> is grey 45% toward bg and <see cref="Palette.Brown"/> is a 60% toward knock.</summary>
    private static Palette Mixed(
        bool isDark, Color bg, Color bar, Color slab, Color white, Color grey,
        Color a, Color knock, Color cyan, Color blue, Color red, Color yellow) => new()
    {
        IsDark = isDark,
        Glows = false,
        Bg = bg,
        Bar = bar,
        Slab = slab,
        White = white,
        Grey = grey,
        Grey2 = Palette.Mix(grey, bg, 0.45),
        A = a,
        Knock = knock,
        Brown = Palette.Mix(a, knock, 0.60),
        Cyan = cyan,
        Blue = blue,
        Red = red,
        Yellow = yellow,
    };
}
