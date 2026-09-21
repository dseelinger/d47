using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Text or a glyph on a solid Accent fill uses D47.Knock, which is Background on a dark
/// theme and its own colour on Light, rather than borrowing D47.Background directly (#344).</summary>
public class KnockIsBackgroundOnDarkAndItsOwnColourOnLightTests
{
    private static ThemeManager Manager() => new(Application.Current!, NullLogger<ThemeManager>.Instance);

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Guardian)]
    public void KnockIsBackgroundOnEveryDarkPalette(string themeId)
    {
        Manager().Apply(themeId);

        var resources = Application.Current!.Resources;

        Assert.Equal(
            ((SolidColorBrush)resources[ThemeManager.BackgroundKey]!).Color,
            ((SolidColorBrush)resources[ThemeManager.KnockKey]!).Color);
    }

    [AvaloniaFact]
    public void KnockIsItsOwnColourOnLight()
    {
        Manager().Apply(ThemeCatalog.Light);

        var knock = ((SolidColorBrush)Application.Current!.Resources[ThemeManager.KnockKey]!).Color;

        Assert.Equal(Color.Parse("#FBF8F2"), knock);
    }
}
