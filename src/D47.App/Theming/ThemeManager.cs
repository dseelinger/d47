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

    /// <summary>Accent at 42% mixed onto Background — captions, unit labels, off-state words (#329).</summary>
    public const string TextFaintKey = "D47.TextFaint";

    public const string AccentKey = "D47.Accent";
    public const string AccentMutedKey = "D47.AccentMuted";

    /// <summary>Text or a glyph drawn on a solid Accent fill: Background on a dark theme, #FBF8F2 on Light.</summary>
    public const string KnockKey = "D47.Knock";

    public const string DangerKey = "D47.Danger";

    /// <summary>Danger's hue at 82 degrees, Accent's lightness and chroma (#329).</summary>
    public const string WarnKey = "D47.Warn";

    /// <summary>Danger's hue at 146 degrees, Accent's lightness and chroma (#329).</summary>
    public const string GoodKey = "D47.Good";

    public const string InfoKey = "D47.Info";

    /// <summary>A 1px rule, 50% of Accent mixed onto Background.</summary>
    public const string RuleKey = "D47.Rule";

    /// <summary>A fill, 9% of Accent mixed onto Background — the unselected tab and secondary button.</summary>
    public const string FillLowKey = "D47.FillLow";

    /// <summary>A fill, 16% of Accent mixed onto Background — the selected list row.</summary>
    public const string FillHighKey = "D47.FillHigh";

    /// <summary>A fill, 27% of Accent mixed onto Background — a stepper's value cell, a binding chip (#329).</summary>
    public const string FillHigherKey = "D47.FillHigher";

    /// <summary>A 1px rule, 35% of Accent mixed onto Background — the ship conversation bubble's border.</summary>
    public const string AccentBorderKey = "D47.AccentBorder";

    /// <summary>Accent blended toward white or near-black — the ship conversation bubble's ink (#329).</summary>
    public const string AccentInkKey = "D47.AccentInk";

    /// <summary>A fill, 9% of <see cref="InfoKey"/> mixed onto Background — the Commander conversation bubble's fill.</summary>
    public const string InfoFillKey = "D47.InfoFill";

    /// <summary>A 1px rule, 35% of <see cref="InfoKey"/> mixed onto Background — the Commander conversation bubble's border.</summary>
    public const string InfoBorderKey = "D47.InfoBorder";

    /// <summary>Info blended into <see cref="TextKey"/> — the Commander conversation bubble's ink.</summary>
    public const string InfoInkKey = "D47.InfoInk";

    /// <summary>A fill, 5% of Accent mixed onto Background — an unselected Fleet card (#278).</summary>
    public const string CardFillKey = "D47.CardFill";

    /// <summary>A fill, 14% of Accent mixed onto Background — the selected Fleet card (#278).</summary>
    public const string CardFillSelectedKey = "D47.CardFillSelected";

    /// <summary>A fill, 5% of Accent mixed onto Background — every other settings row (#279).</summary>
    public const string RowFillKey = "D47.RowFill";

    /// <summary>A 1px rule, 60% of Accent mixed onto Background — a row's inline tag border (#279).</summary>
    public const string TagBorderKey = "D47.TagBorder";

    /// <summary>A 10px glow of <see cref="AccentKey"/> at 34%, behind the marked elements — dark themes only, null in Light (#345).</summary>
    public const string BloomKey = "D47.Bloom";

    /// <summary>A tiled 1px-at-34%-black line brush over the whole window — dark themes only, null in Light (#345).</summary>
    public const string ScanlinesKey = "D47.Scanlines";

    /// <summary>
    /// The transcript pane's fill: Accent from 5% at the top to 1.5% at the bottom on dark themes,
    /// <see cref="SurfaceKey"/>'s colour in Light.
    /// </summary>
    public const string PaneFillKey = "D47.PaneFill";

    /// <summary>A 1px rule, 30% of Accent mixed onto Background — the transcript pane's border.</summary>
    public const string PaneBorderKey = "D47.PaneBorder";

    /// <summary>Accent at 85% mixed onto Background — a bubble's event tag, set as plain text rather than boxed.</summary>
    public const string TagInkKey = "D47.TagInk";

    /// <summary>Black at 50% opacity — a layer chooser's dimming behind its card, in every theme (#325).</summary>
    public const string ScrimKey = "D47.Scrim";

    /// <summary>Every role a theme defines.</summary>
    public static IReadOnlyList<string> Roles { get; } =
    [
        BackgroundKey, SurfaceKey, SurfaceAltKey, BorderKey, TextKey,
        TextMutedKey, TextFaintKey, AccentKey, AccentMutedKey, KnockKey, DangerKey, WarnKey, GoodKey, InfoKey,
        RuleKey, FillLowKey, FillHighKey, FillHigherKey,
        AccentBorderKey, AccentInkKey, InfoFillKey, InfoBorderKey, InfoInkKey,
        CardFillKey, CardFillSelectedKey, RowFillKey, TagBorderKey,
        BloomKey, ScanlinesKey,
        PaneFillKey, PaneBorderKey, TagInkKey, ScrimKey,
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

    /// <param name="matrixOverride">
    /// Used instead of reading the Commander's own HUD matrix from disk — the Control Kit's Accent
    /// entry drives the recolour path this way (#358).
    /// </param>
    public void Apply(string? themeId, GuiColourMatrix? matrixOverride = null)
    {
        var theme = ThemeCatalog.Selected(themeId);
        var palette = Palettes.For(theme.Id);

        if (theme.Id == ThemeCatalog.ElitePaletteId)
        {
            var matrix = matrixOverride ?? ElitePalette.Read(ElitePalette.DefaultPath());

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

        // Every role that mixes or hue-swaps Accent and Background, computed off the (possibly
        // recoloured) Accent so they follow the HUD matrix the same way Accent itself does (#329).
        var derived = DerivedPalette.From(palette);
        var resources = application.Resources;

        resources[BackgroundKey] = new SolidColorBrush(palette.Background);
        resources[ScrimKey] = new SolidColorBrush(Colors.Black, 0.5);
        resources[SurfaceKey] = new SolidColorBrush(derived.Surface);
        resources[SurfaceAltKey] = new SolidColorBrush(derived.SurfaceAlt);
        resources[BorderKey] = new SolidColorBrush(derived.Border);
        resources[TextKey] = new SolidColorBrush(derived.Text);
        resources[TextMutedKey] = new SolidColorBrush(derived.TextMuted);
        resources[TextFaintKey] = new SolidColorBrush(derived.TextFaint);
        resources[AccentKey] = new SolidColorBrush(palette.Accent);
        resources[AccentMutedKey] = new SolidColorBrush(palette.AccentMuted);
        resources[KnockKey] = new SolidColorBrush(palette.IsDark ? palette.Background : Color.Parse("#FBF8F2"));
        resources[DangerKey] = new SolidColorBrush(derived.Danger);
        resources[WarnKey] = new SolidColorBrush(derived.Warn);
        resources[GoodKey] = new SolidColorBrush(derived.Good);
        resources[InfoKey] = new SolidColorBrush(derived.Info);

        resources[RuleKey] = new SolidColorBrush(derived.Rule);
        resources[FillLowKey] = new SolidColorBrush(derived.FillLow);
        resources[FillHighKey] = new SolidColorBrush(derived.FillHigh);
        resources[FillHigherKey] = new SolidColorBrush(derived.FillHigher);

        // The conversation bubbles' own roles (#275): each side's border at 35% of its colour, and an ink
        // blended toward white or near-black so it stays legible on both light and dark themes.
        resources[AccentBorderKey] = new SolidColorBrush(derived.AccentBorder);
        resources[AccentInkKey] = new SolidColorBrush(derived.AccentInk);
        resources[InfoFillKey] = new SolidColorBrush(derived.InfoFill);
        resources[InfoBorderKey] = new SolidColorBrush(derived.InfoBorder);
        resources[InfoInkKey] = new SolidColorBrush(derived.InfoInk);

        // The Fleet card's own fills (#278): unselected at 5% of Accent, selected at 14%.
        resources[CardFillKey] = new SolidColorBrush(derived.CardFill);
        resources[CardFillSelectedKey] = new SolidColorBrush(derived.CardFillSelected);

        // The Settings page's own roles (#279): alternating rows at 5% of Accent, a tag's border at 60%.
        resources[RowFillKey] = new SolidColorBrush(derived.RowFill);
        resources[TagBorderKey] = new SolidColorBrush(derived.TagBorder);

        // Bloom and scanlines (#345): dark themes only, so both resolve to null rather than a brush
        // or effect in Light — which is what turns them off, since an unset Effect or Background
        // paints nothing.
        resources[BloomKey] = palette.IsDark ? Bloom(palette.Accent, 10, 0.34) : null;
        resources[ScanlinesKey] = palette.IsDark ? Scanlines(1) : null;

        // The tint is the pane's, not the page's: the ground behind the pane stays Background.
        resources[PaneFillKey] = palette.IsDark ? PaneFill(palette.Accent) : new SolidColorBrush(derived.Surface);
        resources[PaneBorderKey] = new SolidColorBrush(derived.PaneBorder);
        resources[TagInkKey] = new SolidColorBrush(derived.TagInk);

        // The framework's own controls — text boxes, buttons, scrollbars — follow the variant rather than the
        // palette, so a light theme has to say so or its combo boxes stay dark.
        application.RequestedThemeVariant = palette.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

        logger.LogInformation("Theme is now {Theme}", theme.Name);
    }

    /// <summary>A glow of <paramref name="accent"/>, for the elements named in #345.</summary>
    private static DropShadowEffect Bloom(Color accent, double blurRadius, double opacity) => new()
    {
        Color = accent,
        OffsetX = 0,
        OffsetY = 0,
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
    /// A tiled brush of a 1px black line at 34% alpha every 3px, for the overlay in #345. The pixels are
    /// layout pixels at <paramref name="renderScaling"/>, each rounded to whole screen pixels, so the tile
    /// is drawn one bitmap pixel to one screen pixel.
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
            var ink = (byte)Math.Round(255 * 0.34);

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
