using Avalonia.Media;
using D47.Core.Interface;

namespace D47.App.Theming;

/// <summary>Colour by role, never by name.</summary>
public sealed record Palette
{
    public required bool IsDark { get; init; }

    /// <summary>Whether <see cref="ThemeManager.TextKey"/> is <see cref="Accent"/> rather than <see cref="Text"/>.</summary>
    public required bool InkIsAccent { get; init; }

    /// <summary>The window behind everything, and the base colour every derived role mixes onto.</summary>
    public required Color Background { get; init; }

    /// <summary>The neutral ink, used only where <see cref="InkIsAccent"/> is false.</summary>
    public required Color Text { get; init; }

    /// <summary>The theme's own colour: focus, headings, the ask button, and the other base colour every derived role mixes with.</summary>
    public required Color Accent { get; init; }

    /// <summary>The same colour with the volume down.</summary>
    public required Color AccentMuted { get; init; }

    /// <summary>Recolours the accents through Elite's own HUD matrix.</summary>
    public Palette RecolouredBy(GuiColourMatrix matrix) => this with
    {
        Accent = Transform(matrix, Accent),
        AccentMuted = Transform(matrix, AccentMuted),
    };

    private static Color Transform(GuiColourMatrix matrix, Color colour)
    {
        var (r, g, b) = matrix.Transform(colour.R, colour.G, colour.B);
        return Color.FromArgb(colour.A, r, g, b);
    }
}

/// <summary>The shipped palettes, one per <see cref="ThemeCatalog"/> id.</summary>
public static class Palettes
{
    /// <summary>Amber on black.</summary>
    public static Palette Elite { get; } = new()
    {
        IsDark = true,
        InkIsAccent = true,

        // Black rather than near-black: the Accent tint on a bubble or a pane only reads as a lit panel
        // against a ground with nothing in it, and the scanlines only show on black.
        Background = Color.Parse("#000000"),
        Text = Color.Parse("#E8E2D8"),
        Accent = Color.Parse("#F5850F"),
        AccentMuted = Color.Parse("#A64A00"),
    };

    public static Palette Dark { get; } = new()
    {
        IsDark = true,
        InkIsAccent = false,
        Background = Color.Parse("#121212"),
        Text = Color.Parse("#E6E6E6"),
        Accent = Color.Parse("#4C8DFF"),
        AccentMuted = Color.Parse("#2F5DA8"),
    };

    public static Palette Light { get; } = new()
    {
        IsDark = false,
        InkIsAccent = false,
        Background = Color.Parse("#F4F4F2"),
        Text = Color.Parse("#1A1A1A"),
        Accent = Color.Parse("#0A64C8"),
        AccentMuted = Color.Parse("#5C8FCB"),
    };

    public static Palette Guardian { get; } = new()
    {
        IsDark = true,
        InkIsAccent = true,
        Background = Color.Parse("#06100F"),
        Text = Color.Parse("#DCEFEA"),
        Accent = Color.Parse("#2FD3B5"),
        AccentMuted = Color.Parse("#12796A"),
    };

    /// <summary>The palette for a theme id, before any HUD matrix is applied.</summary>
    public static Palette For(string? themeId) => ThemeCatalog.Selected(themeId).Id switch
    {
        ThemeCatalog.Dark => Dark,
        ThemeCatalog.Light => Light,
        ThemeCatalog.Guardian => Guardian,
        _ => Elite,
    };
}
