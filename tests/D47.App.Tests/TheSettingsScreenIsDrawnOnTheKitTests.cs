using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Settings screen, rendered on each theme and at the three panel sizes and saved to
/// <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03 (#405).
/// </summary>
public class TheSettingsScreenIsDrawnOnTheKitTests
{
    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    private static string Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        var path = Path.Combine(TestSurface.CaptureDirectory, name);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        return path;
    }

    private static SettingsHost Open(double width, double height)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        return SettingsHost.Open(settings, viewState, paths, width: width, height: height);
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite, 1280, 860)]
    [InlineData(ThemeCatalog.Elite, 924, 640)]
    [InlineData(ThemeCatalog.Elite, 512, 280)]
    [InlineData(ThemeCatalog.Dark, 1280, 860)]
    [InlineData(ThemeCatalog.Light, 1280, 860)]
    [InlineData(ThemeCatalog.ElitePaletteId, 1280, 860)]
    public void TheSettingsAreasAreCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        var host = Open(width, height);
        var saved = new List<string>();

        var areas = width == 1280 && themeId == ThemeCatalog.Elite
            ? SettingsLayout.Areas.ToList()
            : [SettingsLayout.Areas[0], SettingsLayout.Areas[^1]];

        foreach (var area in areas)
        {
            host.View.SelectArea(SettingsLayout.Areas.ToList().IndexOf(area));
            saved.Add(Save(host.Window, $"settings-{area.Id}-{themeId}-{width}x{height}.png"));
        }

        host.View.Filter("voice");
        saved.Add(Save(host.Window, $"settings-search-{themeId}-{width}x{height}.png"));

        host.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.All(saved, path => Assert.True(File.Exists(path)));
    }

    /// <summary>
    /// The open place is the Screen title, in sentence case and White at Title size, under its area as an
    /// A breadcrumb.
    /// </summary>
    [AvaloniaFact]
    public void ThePlaceIsTheScreenTitleUnderItsAreasBreadcrumb()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var host = Open(1280, 860);
        var area = SettingsLayout.Areas[0];
        var place = area.Places[0];

        var title = SettingsPageReading.Title(host.View);
        var crumb = SettingsPageReading.Crumb(host.View);

        Assert.Equal(place.Title, title.Text);
        Assert.Equal(Ink(ThemeManager.WhiteKey), (title.Foreground as ISolidColorBrush)?.Color);

        Assert.Equal($"{area.Title.ToUpperInvariant()} ›", crumb.Text);
        Assert.Equal(Ink(ThemeManager.AKey), (crumb.Foreground as ISolidColorBrush)?.Color);

        host.Close();
    }

    /// <summary>A place's rows sit on the page ground under a Line rule.</summary>
    [AvaloniaFact]
    public void RowsSitOnThePageGroundUnderALineRule()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var host = Open(1280, 860);

        var rows = host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass) && grid.IsEffectivelyVisible)
            .Select(grid => grid.GetVisualAncestors().OfType<Border>().First(border => border.MinHeight > 0))
            .ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Null(row.Background));
        Assert.All(rows, row => Assert.DoesNotContain(ListRow.Class, row.Classes));
        Assert.All(rows, row => Assert.Equal(new Avalonia.Thickness(0, 1, 0, 0), row.BorderThickness));
        Assert.All(rows, row => Assert.Equal(Ink(ThemeManager.LineKey), (row.BorderBrush as ISolidColorBrush)?.Color));

        host.Close();
    }

    /// <summary>
    /// The three Voice and hearing pages at full width and at 924, for comparison with the brief: the page
    /// head, no card borders, and nothing wrapping into or clipped by another element.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(1280, 860)]
    [InlineData(924, 640)]
    public void TheVoiceAndHearingPagesAreCaptured(double width, double height)
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var host = Open(width, height);
        var saved = new List<string>();

        foreach (var place in SettingsLayout.Areas[0].Places)
        {
            SettingsPageReading.Open(host.View, place.Id);
            saved.Add(Save(host.Window, $"settings-page-{place.Id}-{width}x{height}.png"));
        }

        host.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.All(saved, path => Assert.True(File.Exists(path)));
    }

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Avalonia.Application.Current!.Resources[key]!).Color;

    [Theory]
    [InlineData("SettingsView.axaml.cs")]
    [InlineData("SettingsView.axaml")]
    [InlineData("SecretEditor.cs")]
    [InlineData("SwitchEditing.cs")]
    [InlineData("LoreEditing.cs")]
    [InlineData("FirstRunWindow.cs")]
    public void TheSettingsSourceDrawsOnlyInTheNewTokens(string file)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "D47.App", "Settings", file));

        Assert.DoesNotContain("CornerRadius", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CardChrome", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"#[0-9A-Fa-f]{6}\b", source);
        Assert.DoesNotMatch(
            @"ThemeManager\.(Background|Surface|SurfaceAlt|Border|Text|TextMuted|TextFaint|Accent|AccentMuted|Danger|Warn|Good|Info|Rule|FillLow|FillHigh|FillHigher|AccentBorder|AccentInk|CardFill|CardFillSelected|RowFill|TagBorder|PaneFill|PaneBorder|TagInk)Key\b",
            source);
        Assert.DoesNotMatch(
            @"D47\.(Background|Surface|SurfaceAlt|Border|Text|TextMuted|TextFaint|Accent|AccentMuted|Danger|Warn|Good|Info|Rule|FillLow|FillHigh|FillHigher|AccentBorder|AccentInk|CardFill|CardFillSelected|RowFill|TagBorder|PaneFill|PaneBorder|TagInk)\}",
            source);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException($"No d47.slnx above {AppContext.BaseDirectory}.");
    }
}
