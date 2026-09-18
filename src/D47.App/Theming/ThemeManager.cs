using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging;

namespace D47.App.Theming;

/// <summary>Publishes the selected palette as application resources and keeps it there.</summary>
public sealed class ThemeManager(Application application, ILogger<ThemeManager> logger)
{
    public const string BackgroundKey = "D47.Background";
    public const string SurfaceKey = "D47.Surface";
    public const string SurfaceAltKey = "D47.SurfaceAlt";
    public const string BorderKey = "D47.Border";
    public const string TextKey = "D47.Text";
    public const string TextMutedKey = "D47.TextMuted";
    public const string AccentKey = "D47.Accent";
    public const string AccentMutedKey = "D47.AccentMuted";
    public const string DangerKey = "D47.Danger";
    public const string InfoKey = "D47.Info";

    /// <summary>A 1px rule, 42% of <see cref="AccentKey"/>.</summary>
    public const string RuleKey = "D47.Rule";

    /// <summary>A fill, 10% of <see cref="AccentKey"/> — the unselected tab and secondary button.</summary>
    public const string FillLowKey = "D47.FillLow";

    /// <summary>A fill, 18% of <see cref="AccentKey"/> — the selected list row.</summary>
    public const string FillHighKey = "D47.FillHigh";

    /// <summary>A 1px rule, 35% of <see cref="AccentKey"/> — the ship conversation bubble's border.</summary>
    public const string AccentBorderKey = "D47.AccentBorder";

    /// <summary>Accent blended into <see cref="TextKey"/> — the ship conversation bubble's ink.</summary>
    public const string AccentInkKey = "D47.AccentInk";

    /// <summary>A fill, 9% of <see cref="InfoKey"/> — the Commander conversation bubble's fill.</summary>
    public const string InfoFillKey = "D47.InfoFill";

    /// <summary>A 1px rule, 35% of <see cref="InfoKey"/> — the Commander conversation bubble's border.</summary>
    public const string InfoBorderKey = "D47.InfoBorder";

    /// <summary>Info blended into <see cref="TextKey"/> — the Commander conversation bubble's ink.</summary>
    public const string InfoInkKey = "D47.InfoInk";

    /// <summary>A fill, 5% of <see cref="AccentKey"/> — an unselected Fleet card (#278).</summary>
    public const string CardFillKey = "D47.CardFill";

    /// <summary>A fill, 14% of <see cref="AccentKey"/> — the selected Fleet card (#278).</summary>
    public const string CardFillSelectedKey = "D47.CardFillSelected";

    /// <summary>A fill, 5% of <see cref="AccentKey"/> — every other settings row (#279).</summary>
    public const string RowFillKey = "D47.RowFill";

    /// <summary>A 1px rule, 60% of <see cref="AccentKey"/> — a row's inline tag border (#279).</summary>
    public const string TagBorderKey = "D47.TagBorder";

    /// <summary>
    /// An 8px glow of <see cref="AccentKey"/> at 40%, behind Accent ink and solid Accent fills — dark
    /// themes only, null in Light (#281).
    /// </summary>
    public const string BloomKey = "D47.Bloom";

    /// <summary>A tiled 1px-at-3.5%-white line brush over the whole window — dark themes only, null in Light (#281).</summary>
    public const string ScanlinesKey = "D47.Scanlines";

    /// <summary>Every role a theme defines.</summary>
    public static IReadOnlyList<string> Roles { get; } =
    [
        BackgroundKey, SurfaceKey, SurfaceAltKey, BorderKey, TextKey,
        TextMutedKey, AccentKey, AccentMutedKey, DangerKey, InfoKey,
        RuleKey, FillLowKey, FillHighKey,
        AccentBorderKey, AccentInkKey, InfoFillKey, InfoBorderKey, InfoInkKey,
        CardFillKey, CardFillSelectedKey, RowFillKey, TagBorderKey,
        BloomKey, ScanlinesKey,
    ];

    /// <summary>Applies the theme named in settings, and re-applies it whenever that setting changes.</summary>
    public void FollowSettings(SettingsService settings)
    {
        Apply(settings.Current.Ui.Theme);

        // Posted, because a settings change does not arrive on the UI thread.
        settings.Changed += change =>
        {
            if (change.Key.Equals(InterfaceCapability.ThemeKey, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(() => Apply(change.Settings.Ui.Theme));
            }
        };
    }

    public void Apply(string? themeId)
    {
        var theme = ThemeCatalog.Selected(themeId);
        var palette = Palettes.For(theme.Id);

        if (theme.Id == ThemeCatalog.ElitePaletteId)
        {
            var matrix = ElitePalette.Read(ElitePalette.DefaultPath());

            if (matrix is null)
            {
                // Fail-soft: plain Elite is what it would have looked like anyway.
                logger.LogInformation(
                    "No usable HUD colour matrix at {Path}; using the Elite palette unchanged",
                    ElitePalette.DefaultPath());
            }
            else
            {
                palette = palette.RecolouredBy(matrix);
                logger.LogInformation("Recoloured the Elite palette from the Commander's HUD matrix");
            }
        }

        var resources = application.Resources;

        resources[BackgroundKey] = new SolidColorBrush(palette.Background);
        resources[SurfaceKey] = new SolidColorBrush(palette.Surface);
        resources[SurfaceAltKey] = new SolidColorBrush(palette.SurfaceAlt);
        resources[BorderKey] = new SolidColorBrush(palette.Border);
        resources[TextKey] = new SolidColorBrush(palette.Text);
        resources[TextMutedKey] = new SolidColorBrush(palette.TextMuted);
        resources[AccentKey] = new SolidColorBrush(palette.Accent);
        resources[AccentMutedKey] = new SolidColorBrush(palette.AccentMuted);
        resources[DangerKey] = new SolidColorBrush(palette.Danger);
        resources[InfoKey] = new SolidColorBrush(palette.Info);

        // Derived from the (possibly recoloured) Accent rather than stored on Palette, so they follow the
        // HUD matrix the same way Accent itself does.
        resources[RuleKey] = new SolidColorBrush(palette.Accent, 0.42);
        resources[FillLowKey] = new SolidColorBrush(palette.Accent, 0.10);
        resources[FillHighKey] = new SolidColorBrush(palette.Accent, 0.18);

        // The conversation bubbles' own roles (#275): each side's border at 35% of its colour, and an ink
        // blended toward Text so it stays legible on both light and dark themes.
        resources[AccentBorderKey] = new SolidColorBrush(palette.Accent, 0.35);
        resources[AccentInkKey] = new SolidColorBrush(Mix(palette.Text, palette.Accent, 0.35));
        resources[InfoFillKey] = new SolidColorBrush(palette.Info, 0.09);
        resources[InfoBorderKey] = new SolidColorBrush(palette.Info, 0.35);
        resources[InfoInkKey] = new SolidColorBrush(Mix(palette.Text, palette.Info, 0.35));

        // The Fleet card's own fills (#278): unselected at 5% of Accent, selected at 14%.
        resources[CardFillKey] = new SolidColorBrush(palette.Accent, 0.05);
        resources[CardFillSelectedKey] = new SolidColorBrush(palette.Accent, 0.14);

        // The Settings page's own roles (#279): alternating rows at 5% of Accent, a tag's border at 60%.
        resources[RowFillKey] = new SolidColorBrush(palette.Accent, 0.05);
        resources[TagBorderKey] = new SolidColorBrush(palette.Accent, 0.60);

        // Bloom and scanlines (#281): dark themes only, so both resolve to null rather than a brush or
        // effect in Light — which is what turns them off, since an unset Effect or Background paints nothing.
        resources[BloomKey] = palette.IsDark ? Bloom(palette.Accent) : null;
        resources[ScanlinesKey] = palette.IsDark ? Scanlines() : null;

        // The framework's own controls — text boxes, buttons, scrollbars — follow the variant rather than the
        // palette, so a light theme has to say so or its combo boxes stay dark.
        application.RequestedThemeVariant = palette.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

        logger.LogInformation("Theme is now {Theme}", theme.Name);
    }

    /// <summary>Blends <paramref name="tint"/> toward <paramref name="text"/> by <paramref name="weight"/>.</summary>
    private static Color Mix(Color text, Color tint, double weight)
    {
        byte Blend(byte from, byte to) => (byte)Math.Round(from + ((to - from) * weight));

        return Color.FromRgb(
            Blend(text.R, tint.R),
            Blend(text.G, tint.G),
            Blend(text.B, tint.B));
    }

    /// <summary>An 8px glow of <paramref name="accent"/> at 40%, for the elements named in #281.</summary>
    private static DropShadowEffect Bloom(Color accent) => new()
    {
        Color = accent,
        OffsetX = 0,
        OffsetY = 0,
        BlurRadius = 8,
        Opacity = 0.4,
    };

    /// <summary>A tiled brush of a 1px line at 3.5% white every 3px, for the overlay in #281.</summary>
    private static ImageBrush Scanlines()
    {
        var size = new PixelSize(1, 3);
        var bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            var line = (byte)Math.Round(255 * 0.035);

            for (var y = 0; y < size.Height; y++)
            {
                var value = y == 0 ? line : (byte)0;
                var row = buffer.Address + (y * buffer.RowBytes);

                for (var channel = 0; channel < 4; channel++)
                {
                    Marshal.WriteByte(row + channel, value);
                }
            }
        }

        return new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            SourceRect = new RelativeRect(0, 0, 1, 3, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, 1, 3, RelativeUnit.Absolute),
        };
    }
}
