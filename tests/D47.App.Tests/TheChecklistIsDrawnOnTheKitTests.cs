using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Goals;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Checklist, its goals band and its suggestions, rendered on each theme and at the three panel sizes
/// and saved to <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03 (#402).
/// </summary>
public class TheChecklistIsDrawnOnTheKitTests
{
    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    private sealed record Surface(Window Window, PanelView Panel, ChecklistService Checklists);

    private static Surface Open(double width, double height)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-checklist-capture"));
        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        checklists.AddNote(ChecklistScope.Universal, "Buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "Get a new paint job");
        checklists.AddNote(ChecklistScope.Universal, "Sell the exploration data");
        checklists.Complete(checklists.Document.Items.Single(item => item.Text == "Get a new paint job").Id);
        checklists.Select(checklists.Document.Items.Single(item => item.Text == "Buy limpets").Id);
        checklists.ProposeAdd(ChecklistScope.Universal, ["Run to the supermarket"]);

        var goals = new GoalBook(
            new GoalStore(Path.Combine(paths.Data, "goals.json"), NullLogger<GoalStore>.Instance),
            () => null,
            () => null,
            checklists);

        var panel = new PanelView
        {
            DataContext = new PanelViewModel(),
            Mode = height < 400 ? PanelMode.Mini : PanelMode.Full,
        };

        panel.EnableChecklist(checklists, goals);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, checklists);
    }

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

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite, 1280, 860)]
    [InlineData(ThemeCatalog.Elite, 924, 640)]
    [InlineData(ThemeCatalog.Elite, 512, 280)]
    [InlineData(ThemeCatalog.Dark, 1280, 860)]
    [InlineData(ThemeCatalog.Light, 1280, 860)]
    [InlineData(ThemeCatalog.ElitePaletteId, 1280, 860)]
    public void TheChecklistIsCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.Put(themeId, themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        var surface = Open(width, height);
        var panel = surface.Panel;

        var list = Save(surface.Window, $"checklist-{themeId}-{width}x{height}.png");

        var band = panel.GetVisualDescendants().OfType<CheckBox>()
            .First(box => box.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text?.StartsWith("Goals", StringComparison.Ordinal) == true));

        band.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        var goals = Save(surface.Window, $"checklist-goals-{themeId}-{width}x{height}.png");

        panel.Nav.Drill(new NavCrumb(ChecklistPage.SuggestionsKey, "Suggestions"));
        var suggestions = Save(surface.Window, $"checklist-suggestions-{themeId}-{width}x{height}.png");

        surface.Window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(File.Exists(list));
        Assert.True(File.Exists(goals));
        Assert.True(File.Exists(suggestions));
    }

    /// <summary>A tab's own settings strip, closed and open, on Elite.</summary>
    [AvaloniaFact]
    public void ThePageSettingsStripIsCaptured()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (settings, viewState, paths) = TestSurface.Create();
        var view = new SettingsView();
        view.Attach(settings, viewState, paths, tabPlaceId: "fleet-ships");

        var window = new Window { Content = view, Width = 924, Height = 400 };
        window.Show();

        var closed = Save(window, "checklist-settings-strip-closed.png");

        var strip = (StackPanel)view.GetVisualDescendants().First(c => c.Name == SettingsView.TabStripName);
        ((Button)strip.Children[0]).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var open = Save(window, "checklist-settings-strip-open.png");

        window.Close();

        Assert.True(File.Exists(closed));
        Assert.True(File.Exists(open));
    }

    /// <summary>The selected line is a solid A fill, and the others the list row's Tile.</summary>
    [AvaloniaFact]
    public void TheSelectedLineIsFilledNotOutlined()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(1280, 860);

        var rows = surface.Panel.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains(ListRow.Class))
            .ToList();

        var selected = Assert.Single(rows, row => row.Classes.Contains(ListRow.SelectedClass));

        Assert.Equal(Ink(ThemeManager.AKey), (selected.Background as ISolidColorBrush)?.Color);
        Assert.All(rows, row => Assert.Equal(default, row.BorderThickness));

        surface.Window.Close();
    }

    /// <summary>Delete completed items is a destructive button at the kit's height.</summary>
    [AvaloniaFact]
    public void DeleteCompletedIsADestructiveButton()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(1280, 860);

        var delete = surface.Panel.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Content as string == "Delete completed items");

        var suggestions = surface.Panel.GetVisualDescendants().OfType<Button>()
            .Single(button => (button.Content as string)?.StartsWith("Suggestions", StringComparison.Ordinal) == true);

        Assert.Contains("destructive", delete.Classes);
        Assert.Equal(suggestions.Bounds.Height, delete.Bounds.Height);
        Assert.InRange(delete.Bounds.Height, 44, 48);

        surface.Window.Close();
    }

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Avalonia.Application.Current!.Resources[key]!).Color;

    [Fact]
    public void TheChecklistSourceDrawsOnlyInTheNewTokens()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "D47.App", "Panel", "ChecklistPage.cs"));

        Assert.DoesNotContain("CornerRadius", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CardChrome", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"#[0-9A-Fa-f]{6}\b", source);
        Assert.DoesNotMatch(
            @"ThemeManager\.(Background|Surface|SurfaceAlt|Border|Text|TextMuted|TextFaint|Accent|AccentMuted|Danger|Warn|Good|Info|Rule|FillLow|FillHigh|FillHigher|AccentBorder|AccentInk|CardFill|CardFillSelected|RowFill|TagBorder|PaneFill|PaneBorder|TagInk)Key\b",
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
