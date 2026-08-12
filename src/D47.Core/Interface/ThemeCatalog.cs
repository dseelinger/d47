namespace D47.Core.Interface;

public sealed record ThemeInfo(string Id, string Name, string Summary);

/// <summary>
/// The themes d47 ships. Identity only — the colours themselves live in one place in the UI
/// layer, which is what "no view hardcodes a literal" means in practice (list.md Phase 4).
/// Core knows a theme's name because a settings row has to offer it; it does not know what
/// colour anything is, because Core does not know what a colour is.
/// </summary>
public static class ThemeCatalog
{
    public const string Dark = "dark";

    public const string Light = "light";

    public const string Elite = "elite";

    public const string Guardian = "guardian";

    /// <summary>Derived from the Commander's own HUD matrix — see <see cref="ElitePalette"/>.</summary>
    public const string ElitePaletteId = "elite-palette";

    public static IReadOnlyList<ThemeInfo> All { get; } =
    [
        new(Elite, "Elite", "Amber on near-black. The default, and the one that matches the cockpit."),
        new(Dark, "Dark", "Neutral dark. No colour opinion."),
        new(Light, "Light", "Neutral light, for a desktop that is not in a dark room."),
        new(Guardian, "Guardian", "Guardian teal and gold."),
        new(ElitePaletteId, "Elite colour scheme", "Elite, recoloured by your own HUD matrix if you have one."),
    ];

    public static IReadOnlyList<string> Ids { get; } = [.. All.Select(t => t.Id)];

    public static ThemeInfo Selected(string? id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
