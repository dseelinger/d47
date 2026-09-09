using Avalonia.Media;
using D47.Core.Interface;

namespace D47.App.Theming;

/// <summary>Colour by role, never by name.</summary>
public sealed record Palette
{
    public required bool IsDark { get; init; }

    /// <summary>The window behind everything.</summary>
    public required Color Background { get; init; }

    /// <summary>Cards, the transcript, raised areas.</summary>
    public required Color Surface { get; init; }

    /// <summary>Row striping and inset areas.</summary>
    public required Color SurfaceAlt { get; init; }

    /// <summary>Hairlines between things.</summary>
    public required Color Border { get; init; }

    public required Color Text { get; init; }

    /// <summary>Help text, placeholders, the per-turn provenance line.</summary>
    public required Color TextMuted { get; init; }

    /// <summary>The theme's own colour: focus, headings, the ask button.</summary>
    public required Color Accent { get; init; }

    /// <summary>The same colour with the volume down.</summary>
    public required Color AccentMuted { get; init; }

    public required Color Danger { get; init; }

    public required Color Info { get; init; }

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
    /// <summary>Amber on near-black.</summary>
    public static Palette Elite { get; } = new()
    {
        IsDark = true,
        Background = Color.Parse("#0B0B0D"),
        Surface = Color.Parse("#15151A"),
        SurfaceAlt = Color.Parse("#1E1E24"),
        Border = Color.Parse("#2E2E36"),
        Text = Color.Parse("#E8E2D8"),
        TextMuted = Color.Parse("#9A9288"),
        Accent = Color.Parse("#FF7100"),
        AccentMuted = Color.Parse("#A64A00"),
        Danger = Color.Parse("#FF5555"),
        Info = Color.Parse("#2288FF"),
    };

    public static Palette Dark { get; } = new()
    {
        IsDark = true,
        Background = Color.Parse("#121212"),
        Surface = Color.Parse("#1C1C1C"),
        SurfaceAlt = Color.Parse("#242424"),
        Border = Color.Parse("#333333"),
        Text = Color.Parse("#E6E6E6"),
        TextMuted = Color.Parse("#9E9E9E"),
        Accent = Color.Parse("#4C8DFF"),
        AccentMuted = Color.Parse("#2F5DA8"),
        Danger = Color.Parse("#FF5555"),
        Info = Color.Parse("#2288FF"),
    };

    public static Palette Light { get; } = new()
    {
        IsDark = false,
        Background = Color.Parse("#F4F4F2"),
        Surface = Color.Parse("#FFFFFF"),
        SurfaceAlt = Color.Parse("#ECECEA"),
        Border = Color.Parse("#D6D6D2"),
        Text = Color.Parse("#1A1A1A"),
        TextMuted = Color.Parse("#5F5F5F"),
        Accent = Color.Parse("#0A64C8"),
        AccentMuted = Color.Parse("#5C8FCB"),
        Danger = Color.Parse("#C62828"),
        Info = Color.Parse("#1565C0"),
    };

    public static Palette Guardian { get; } = new()
    {
        IsDark = true,
        Background = Color.Parse("#06100F"),
        Surface = Color.Parse("#0E1A19"),
        SurfaceAlt = Color.Parse("#142523"),
        Border = Color.Parse("#1F3230"),
        Text = Color.Parse("#DCEFEA"),
        TextMuted = Color.Parse("#7FA79F"),
        Accent = Color.Parse("#2FD3B5"),
        AccentMuted = Color.Parse("#12796A"),
        Danger = Color.Parse("#FF6B6B"),
        Info = Color.Parse("#45B0FF"),
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
