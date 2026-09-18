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

    /// <summary>A 2px rule, 70% of <see cref="AccentKey"/> — under the tab strip, in every theme (#285).</summary>
    public const string TabStripRuleKey = "D47.TabStripRule";

    /// <summary>An 18px glow of <see cref="AccentKey"/> at 38%, behind a solid Accent fill — dark themes only, null in Light (#285).</summary>
    public const string BloomFillKey = "D47.Bloom.Fill";

    /// <summary>The headset's stronger reading of <see cref="BloomFillKey"/>: 34px at 60% (#285).</summary>
    public const string BloomFillHeadsetKey = "D47.Bloom.Fill.Headset";

    /// <summary>A 16px glow of <see cref="AccentKey"/> at 22%, offset 2px down, behind the tab-strip rule — dark themes only, null in Light (#285).</summary>
    public const string BloomRuleKey = "D47.Bloom.Rule";

    /// <summary>The headset's stronger reading of <see cref="BloomRuleKey"/>: 28px at 45% (#285).</summary>
    public const string BloomRuleHeadsetKey = "D47.Bloom.Rule.Headset";

    /// <summary>A 22px glow of <see cref="AccentKey"/> at 12%, behind the panel's outer edge — dark themes only, null in Light (#285).</summary>
    public const string BloomEdgeKey = "D47.Bloom.Edge";

    /// <summary>The headset's stronger reading of <see cref="BloomEdgeKey"/>: 44px at 30% (#285).</summary>
    public const string BloomEdgeHeadsetKey = "D47.Bloom.Edge.Headset";

    /// <summary>A tiled 1px-at-3.5%-white line brush over the whole window — dark themes only, null in Light (#281).</summary>
    public const string ScanlinesKey = "D47.Scanlines";

    /// <summary>
    /// The transcript pane's fill: Accent from 5% at the top to 1.5% at the bottom on dark themes,
    /// <see cref="SurfaceKey"/>'s colour in Light.
    /// </summary>
    public const string PaneFillKey = "D47.PaneFill";

    /// <summary>A 1px rule, 30% of <see cref="AccentKey"/> — the transcript pane's border.</summary>
    public const string PaneBorderKey = "D47.PaneBorder";

    /// <summary>Accent at 85% — a bubble's event tag, set as plain text rather than boxed.</summary>
    public const string TagInkKey = "D47.TagInk";

    /// <summary>Every role a theme defines.</summary>
    public static IReadOnlyList<string> Roles { get; } =
    [
        BackgroundKey, SurfaceKey, SurfaceAltKey, BorderKey, TextKey,
        TextMutedKey, AccentKey, AccentMutedKey, DangerKey, InfoKey,
        RuleKey, FillLowKey, FillHighKey,
        AccentBorderKey, AccentInkKey, InfoFillKey, InfoBorderKey, InfoInkKey,
        CardFillKey, CardFillSelectedKey, RowFillKey, TagBorderKey, TabStripRuleKey,
        BloomFillKey, BloomFillHeadsetKey, BloomRuleKey, BloomRuleHeadsetKey, BloomEdgeKey, BloomEdgeHeadsetKey,
        ScanlinesKey,
        PaneFillKey, PaneBorderKey, TagInkKey,
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

        // On a dark theme the ship's ink is Accent lifted toward white — #FFB066 from Elite's #F5850F, 8.4:1 on
        // the bubble. Blending toward Text instead pulled it most of the way to Text's own warm grey.
        resources[AccentInkKey] = new SolidColorBrush(palette.IsDark
            ? Mix(Colors.White, palette.Accent, 0.65)
            : Mix(palette.Text, palette.Accent, 0.35));
        resources[InfoFillKey] = new SolidColorBrush(palette.Info, 0.09);
        resources[InfoBorderKey] = new SolidColorBrush(palette.Info, 0.35);
        resources[InfoInkKey] = new SolidColorBrush(Mix(palette.Text, palette.Info, 0.35));

        // The Fleet card's own fills (#278): unselected at 5% of Accent, selected at 14%.
        resources[CardFillKey] = new SolidColorBrush(palette.Accent, 0.05);
        resources[CardFillSelectedKey] = new SolidColorBrush(palette.Accent, 0.14);

        // The Settings page's own roles (#279): alternating rows at 5% of Accent, a tag's border at 60%.
        resources[RowFillKey] = new SolidColorBrush(palette.Accent, 0.05);
        resources[TagBorderKey] = new SolidColorBrush(palette.Accent, 0.60);

        // The tab-strip rule (#285): drawn in every theme, unlike bloom, which only glows around it.
        resources[TabStripRuleKey] = new SolidColorBrush(palette.Accent, 0.70);

        // Bloom and scanlines (#281, recalibrated #285): dark themes only, so both resolve to null
        // rather than a brush or effect in Light — which is what turns them off, since an unset Effect
        // or Background paints nothing. A glow shows only where the area around it is dark, so the
        // headset's copy runs stronger values than the desktop's rather than the same ones (#285).
        resources[BloomFillKey] = palette.IsDark ? Bloom(palette.Accent, 18, 0.38) : null;
        resources[BloomFillHeadsetKey] = palette.IsDark ? Bloom(palette.Accent, 34, 0.60) : null;
        resources[BloomRuleKey] = palette.IsDark ? Bloom(palette.Accent, 16, 0.22, offsetY: 2) : null;
        resources[BloomRuleHeadsetKey] = palette.IsDark ? Bloom(palette.Accent, 28, 0.45, offsetY: 2) : null;
        resources[BloomEdgeKey] = palette.IsDark ? Bloom(palette.Accent, 22, 0.12) : null;
        resources[BloomEdgeHeadsetKey] = palette.IsDark ? Bloom(palette.Accent, 44, 0.30) : null;
        resources[ScanlinesKey] = palette.IsDark ? Scanlines(1) : null;

        // The tint is the pane's, not the page's: the ground behind the pane stays Background.
        resources[PaneFillKey] = palette.IsDark ? PaneFill(palette.Accent) : new SolidColorBrush(palette.Surface);
        resources[PaneBorderKey] = new SolidColorBrush(palette.Accent, 0.30);
        resources[TagInkKey] = new SolidColorBrush(palette.Accent, 0.85);

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

    /// <summary>A glow of <paramref name="accent"/>, for the elements named in #285.</summary>
    private static DropShadowEffect Bloom(Color accent, double blurRadius, double opacity, double offsetY = 0) => new()
    {
        Color = accent,
        OffsetX = 0,
        OffsetY = offsetY,
        BlurRadius = blurRadius,
        Opacity = opacity,
    };

    /// <summary>Accent from 5% at the top to 1.5% at the bottom, top to bottom of whatever it fills.</summary>
    private static LinearGradientBrush PaneFill(Color accent) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(13, accent.R, accent.G, accent.B), 0),
            new GradientStop(Color.FromArgb(4, accent.R, accent.G, accent.B), 1),
        },
    };

    /// <summary>
    /// A tiled brush of a 1px line at 3.5% white every 3px, for the overlay in #281. The pixels are layout
    /// pixels at <paramref name="renderScaling"/>, each rounded to whole screen pixels, so the tile is drawn
    /// one bitmap pixel to one screen pixel.
    /// </summary>
    /// <remarks>
    /// A 1px line every 3 screen pixels is too fine to see at 150% or 200%; a tile laid out in unrounded
    /// layout pixels is resampled into a grey smear at 125% and 150%.
    /// </remarks>
    public static ImageBrush Scanlines(double renderScaling)
    {
        var line = Math.Max(1, (int)Math.Round(renderScaling));
        var size = new PixelSize(1, Math.Max(line + 1, (int)Math.Round(3 * renderScaling)));
        var dpi = 96 * renderScaling;
        var bitmap = new WriteableBitmap(size, new Vector(dpi, dpi), PixelFormat.Bgra8888, AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            var ink = (byte)Math.Round(255 * 0.035);

            for (var y = 0; y < size.Height; y++)
            {
                var value = y < line ? ink : (byte)0;
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
            Stretch = Stretch.Fill,
            SourceRect = new RelativeRect(0, 0, 1, 1, RelativeUnit.Relative),
            DestinationRect = new RelativeRect(
                0, 0, size.Width / renderScaling, size.Height / renderScaling, RelativeUnit.Absolute),
        };
    }
}
