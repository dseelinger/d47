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
    public const string BgKey = "D47.Bg";
    public const string BarKey = "D47.Bar";
    public const string SlabKey = "D47.Slab";
    public const string WhiteKey = "D47.White";
    public const string GreyKey = "D47.Grey";
    public const string Grey2Key = "D47.Grey2";
    public const string AKey = "D47.A";
    public const string KnockKey = "D47.Knock";
    public const string BrownKey = "D47.Brown";
    public const string CyanKey = "D47.Cyan";
    public const string BlueKey = "D47.Blue";
    public const string RedKey = "D47.Red";
    public const string YellowKey = "D47.Yellow";
    public const string TileKey = "D47.Tile";
    public const string Tile2Key = "D47.Tile2";
    public const string LineKey = "D47.Line";
    public const string Line2Key = "D47.Line2";

    // Legacy keys, each published as the token named beside it.
    public const string BackgroundKey = "D47.Background"; // bg
    public const string SurfaceKey = "D47.Surface"; // bar
    public const string SurfaceAltKey = "D47.SurfaceAlt"; // slab
    public const string BorderKey = "D47.Border"; // line2
    public const string TextKey = "D47.Text"; // white
    public const string TextMutedKey = "D47.TextMuted"; // grey
    public const string TextFaintKey = "D47.TextFaint"; // grey2
    public const string AccentKey = "D47.Accent"; // a
    public const string AccentMutedKey = "D47.AccentMuted"; // line
    public const string DangerKey = "D47.Danger"; // red
    public const string WarnKey = "D47.Warn"; // a
    public const string GoodKey = "D47.Good"; // blue
    public const string InfoKey = "D47.Info"; // blue
    public const string RuleKey = "D47.Rule"; // line
    public const string FillLowKey = "D47.FillLow"; // tile
    public const string FillHighKey = "D47.FillHigh"; // tile2
    public const string FillHigherKey = "D47.FillHigher"; // slab
    public const string AccentBorderKey = "D47.AccentBorder"; // line2
    public const string AccentInkKey = "D47.AccentInk"; // white
    public const string CardFillKey = "D47.CardFill"; // tile
    public const string CardFillSelectedKey = "D47.CardFillSelected"; // tile2
    public const string RowFillKey = "D47.RowFill"; // tile
    public const string TagBorderKey = "D47.TagBorder"; // line
    public const string PaneFillKey = "D47.PaneFill"; // bg
    public const string PaneBorderKey = "D47.PaneBorder"; // line2
    public const string TagInkKey = "D47.TagInk"; // a

    /// <summary><see cref="Palette.CyanGround"/>.</summary>
    public const string CyanGroundKey = "D47.CyanGround";

    /// <summary>Black at 72% — a layer chooser's dimming behind its card, in every theme.</summary>
    public const string ScrimKey = "D47.Scrim";

    /// <summary>A tiled brush of a 1px black line at 30% every 3px over the whole window; null on a theme that does not glow.</summary>
    public const string ScanlinesKey = "D47.Scanlines";

    /// <summary>The opacity of a scanline layer as a whole.</summary>
    public const double ScanlinesOpacity = 0.5;

    /// <summary>The token keys, in table order.</summary>
    public static IReadOnlyList<string> Tokens { get; } =
    [
        BgKey, BarKey, SlabKey, WhiteKey, GreyKey, Grey2Key, AKey, KnockKey, BrownKey,
        CyanKey, BlueKey, RedKey, YellowKey, TileKey, Tile2Key, LineKey, Line2Key,
    ];

    /// <summary>Every role a theme defines.</summary>
    public static IReadOnlyList<string> Roles { get; } =
    [
        .. Tokens,
        .. Legacy(Palettes.Elite).Keys,
        .. BloomStopKeys(), ScanlinesKey, ScrimKey, CyanGroundKey,
    ];

    /// <summary>Each token key and its colour in <paramref name="palette"/>.</summary>
    public static IReadOnlyDictionary<string, Color> TokenColours(Palette palette) => new Dictionary<string, Color>
    {
        [BgKey] = palette.Bg,
        [BarKey] = palette.Bar,
        [SlabKey] = palette.Slab,
        [WhiteKey] = palette.White,
        [GreyKey] = palette.Grey,
        [Grey2Key] = palette.Grey2,
        [AKey] = palette.A,
        [KnockKey] = palette.Knock,
        [BrownKey] = palette.Brown,
        [CyanKey] = palette.Cyan,
        [BlueKey] = palette.Blue,
        [RedKey] = palette.Red,
        [YellowKey] = palette.Yellow,
        [TileKey] = palette.Tile,
        [Tile2Key] = palette.Tile2,
        [LineKey] = palette.Line,
        [Line2Key] = palette.Line2,
    };

    /// <summary>Each legacy key and the token colour it is published as.</summary>
    public static IReadOnlyDictionary<string, Color> Legacy(Palette palette) => new Dictionary<string, Color>
    {
        [BackgroundKey] = palette.Bg,
        [SurfaceKey] = palette.Bar,
        [SurfaceAltKey] = palette.Slab,
        [BorderKey] = palette.Line2,
        [TextKey] = palette.White,
        [TextMutedKey] = palette.Grey,
        [TextFaintKey] = palette.Grey2,
        [AccentKey] = palette.A,
        [AccentMutedKey] = palette.Line,
        [DangerKey] = palette.Red,
        [WarnKey] = palette.A,
        [GoodKey] = palette.Blue,
        [InfoKey] = palette.Blue,
        [RuleKey] = palette.Line,
        [FillLowKey] = palette.Tile,
        [FillHighKey] = palette.Tile2,
        [FillHigherKey] = palette.Slab,
        [AccentBorderKey] = palette.Line2,
        [AccentInkKey] = palette.White,
        [CardFillKey] = palette.Tile,
        [CardFillSelectedKey] = palette.Tile2,
        [RowFillKey] = palette.Tile,
        [TagBorderKey] = palette.Line,
        [PaneFillKey] = palette.Bg,
        [PaneBorderKey] = palette.Line2,
        [TagInkKey] = palette.A,
    };

    /// <summary>
    /// One stop of a tier's glow: a <see cref="DropShadowEffect"/> of <see cref="Palette.A"/>, drawn by a
    /// <see cref="BloomStack"/> ghost. Null on a theme that does not glow and past the stops the amount emits.
    /// </summary>
    public static string BloomStopKey(BloomTier tier, int stop) => $"D47.Bloom.{tier}.{stop}";

    private static IEnumerable<string> BloomStopKeys() =>
        Enum.GetValues<BloomTier>().SelectMany(tier =>
            Enumerable.Range(0, BloomTiers.Table(tier).Count).Select(stop => BloomStopKey(tier, stop)));

    /// <summary>The palette a theme id resolves to, with the Commander's HUD matrix applied where the theme takes one.</summary>
    public Palette Resolve(string? themeId, GuiColourMatrix? matrixOverride = null)
    {
        var theme = ThemeCatalog.Selected(themeId);
        var palette = Palettes.For(theme.Id);

        if (theme.Id != ThemeCatalog.ElitePaletteId)
        {
            return palette;
        }

        var matrix = matrixOverride ?? ElitePalette.Read(ElitePalette.DefaultPath());

        if (matrix is null)
        {
            // Fail-soft: plain Elite is what it would have looked like anyway.
            logger.LogInformation(
                "No usable HUD colour matrix at {Path}; using the Elite palette unchanged",
                ElitePalette.DefaultPath());

            return palette;
        }

        logger.LogInformation("Recoloured the Elite palette from the Commander's HUD matrix");
        return palette.RecolouredBy(matrix);
    }

    /// <summary>
    /// Applies the theme and bloom amount named in settings, and re-applies them whenever either
    /// setting changes.
    /// </summary>
    public void FollowSettings(SettingsService settings)
    {
        Apply(settings.Current.Ui.Theme, bloomAmount: settings.Current.Ui.BloomAmount);

        // Posted, because a settings change does not arrive on the UI thread.
        settings.Changed += change =>
        {
            if (change.Key.Equals(InterfaceCapability.ThemeKey, StringComparison.OrdinalIgnoreCase)
                || change.Key.Equals(InterfaceCapability.BloomKey, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(
                    () => Apply(change.Settings.Ui.Theme, bloomAmount: change.Settings.Ui.BloomAmount));
            }
        };
    }

    /// <param name="matrixOverride">
    /// Used instead of reading the Commander's own HUD matrix from disk — the Control Kit's Accent
    /// entry drives the recolour path this way (#358).
    /// </param>
    /// <param name="bloomAmount">
    /// How wide the glow halos draw, on <see cref="BloomTiers"/>'s scale (#378).
    /// </param>
    public void Apply(string? themeId, GuiColourMatrix? matrixOverride = null, double bloomAmount = BloomTiers.DefaultAmount)
    {
        var theme = ThemeCatalog.Selected(themeId);
        var palette = Resolve(themeId, matrixOverride);
        var resources = application.Resources;

        foreach (var (key, colour) in TokenColours(palette).Concat(Legacy(palette)))
        {
            resources[key] = new SolidColorBrush(colour);
        }

        resources[ScrimKey] = new SolidColorBrush(Colors.Black, 0.72);
        resources[CyanGroundKey] = new SolidColorBrush(palette.CyanGround);

        // Null on a theme that does not glow, which turns both off: an unset Effect or Background paints nothing.
        foreach (var tier in Enum.GetValues<BloomTier>())
        {
            var stops = palette.Glows ? BloomTiers.Stops(tier, bloomAmount) : [];

            for (var stop = 0; stop < BloomTiers.Table(tier).Count; stop++)
            {
                resources[BloomStopKey(tier, stop)] = stop < stops.Count ? Bloom(palette.A, stops[stop]) : null;
            }
        }

        resources[ScanlinesKey] = palette.Glows ? Scanlines(1) : null;

        // The framework's own controls — text boxes, buttons, scrollbars — follow the variant rather than the
        // palette, so a light theme has to say so or its combo boxes stay dark.
        application.RequestedThemeVariant = palette.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

        logger.LogInformation("Theme is now {Theme}", theme.Name);
    }

    /// <summary>One stop of a glow of <paramref name="accent"/>, spread as the stop's CSS radius would spread it.</summary>
    private static DropShadowEffect Bloom(Color accent, BloomStop stop) => new()
    {
        Color = accent,
        OffsetX = 0,
        OffsetY = 0,
        BlurRadius = SkiaBlurRadius(stop.Radius),
        Opacity = stop.Alpha,
    };

    /// <summary>
    /// The <see cref="DropShadowEffect.BlurRadius"/> that spreads as far as a CSS blur radius:
    /// CSS blurs at σ = r / 2, Avalonia.Skia at σ = 0.288675 × BlurRadius + 0.5.
    /// </summary>
    public static double SkiaBlurRadius(double cssRadius) => Math.Max(0, (cssRadius / 2 - 0.5) / 0.288675);

    /// <summary>
    /// A tiled brush of a 1px black line at 30% alpha every 3px. The pixels are layout pixels at
    /// <paramref name="renderScaling"/>, each rounded to whole screen pixels, so the tile is drawn one
    /// bitmap pixel to one screen pixel.
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
            var ink = (byte)Math.Round(255 * 0.30);

            for (var y = 0; y < size.Height; y++)
            {
                var alpha = y < line ? ink : (byte)0;
                var row = buffer.Address + (y * buffer.RowBytes);

                for (var channel = 0; channel < 3; channel++)
                {
                    Marshal.WriteByte(row + channel, 0);
                }

                Marshal.WriteByte(row + 3, alpha);
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
