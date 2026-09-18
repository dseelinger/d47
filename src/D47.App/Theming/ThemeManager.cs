using Avalonia;
using Avalonia.Media;
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

    /// <summary>Every role a theme defines.</summary>
    public static IReadOnlyList<string> Roles { get; } =
    [
        BackgroundKey, SurfaceKey, SurfaceAltKey, BorderKey, TextKey,
        TextMutedKey, AccentKey, AccentMutedKey, DangerKey, InfoKey,
        RuleKey, FillLowKey, FillHighKey,
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

        // The framework's own controls — text boxes, buttons, scrollbars — follow the variant rather than the
        // palette, so a light theme has to say so or its combo boxes stay dark.
        application.RequestedThemeVariant = palette.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

        logger.LogInformation("Theme is now {Theme}", theme.Name);
    }
}
