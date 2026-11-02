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
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Checklist, its goals band and its suggestions, rendered on each theme and at the three panel sizes
/// and saved to <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03 (#402).
/// </summary>
[Trait("Category", "Integration")]
public class TheChecklistIsDrawnOnTheKitTests
{
    private sealed record Surface(Window Window, PanelView Panel, ChecklistService Checklists);

    private static Surface Open(double width, double height)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-checklist-capture"));
        paths.EnsureCreated();

        var checklists = TestSurface.Checklists(paths.Data);

        checklists.AddNote(ChecklistScope.Universal, "Buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "Get a new paint job");
        checklists.AddNote(ChecklistScope.Universal, "Sell the exploration data");
        checklists.Complete(checklists.Document.Items.Single(item => item.Text == "Get a new paint job").Id);
        checklists.Select(checklists.Document.Items.Single(item => item.Text == "Buy limpets").Id);
        checklists.ProposeAdd(ChecklistScope.Universal, ["Run to the supermarket"]);

        var goals = new GoalBook(
            new GoalStore(Path.Combine(paths.Data, "goals.json"), new MemoryFileSystem(), NullLogger<GoalStore>.Instance),
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

        panel.Tab = PanelTab.Commander;
        panel.Nav.Drill(ChecklistPage.AllLists);
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, checklists);
    }

    [Trait("Category", "Integration")]
    [AvaloniaTheory]
    [MemberData(nameof(CaptureMatrix.Rows), MemberType = typeof(CaptureMatrix))]
    public void TheChecklistIsCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.PutForCapture(themeId);

        var surface = Open(width, height);
        var panel = surface.Panel;

        var list = surface.Window.SaveFrame($"checklist-{themeId}-{width}x{height}.png");

        panel.Nav.ToRoot();
        var lists = surface.Window.SaveFrame($"checklist-lists-{themeId}-{width}x{height}.png");

        panel.Nav.Drill(ChecklistPage.ListCrumb(surface.Checklists.Lists()[0]));
        var one = surface.Window.SaveFrame($"checklist-one-{themeId}-{width}x{height}.png");

        panel.Nav.ToRoot();
        Dispatcher.UIThread.RunJobs();

        var band = panel.GetVisualDescendants().OfType<CheckBox>()
            .First(box => box.Content is string label && label.StartsWith("Goals", StringComparison.Ordinal));

        band.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        var goals = surface.Window.SaveFrame($"checklist-goals-{themeId}-{width}x{height}.png");

        panel.Nav.Drill(new NavCrumb(ChecklistPage.SuggestionsKey, "Suggestions"));
        var suggestions = surface.Window.SaveFrame($"checklist-suggestions-{themeId}-{width}x{height}.png");

        surface.Window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A tab's own settings strip, closed and open, on Elite.</summary>
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ThePageSettingsStripIsCaptured()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (settings, viewState, paths) = TestSurface.Create();
        var view = new SettingsView();
        view.Attach(settings, viewState, tabPlaceId: "fleet-ships");

        var window = new Window { Content = view, Width = 924, Height = 400 };
        window.Show();

        var closed = window.SaveFrame("checklist-settings-strip-closed.png");

        ((Button)view.BarTool!).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var open = window.SaveFrame("checklist-settings-strip-open.png");

        window.Close();
    }

    /// <summary>The selected line is a solid A fill, and the others the list row's Tile.</summary>
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void DeleteCompletedIsADestructiveButton()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(1280, 860);

        var delete = surface.Panel.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Content as string == "Delete completed");

        var height = delete.Bounds.Height;

        Assert.Contains("destructive", delete.Classes);

        // Suggestions is on the list of lists, one level up.
        surface.Panel.Nav.ToRoot();
        Dispatcher.UIThread.RunJobs();

        var suggestions = surface.Panel.GetVisualDescendants().OfType<Button>()
            .Single(button => (button.Content as string)?.StartsWith("Suggestions", StringComparison.Ordinal) == true);

        Assert.Equal(suggestions.Bounds.Height, height);
        Assert.InRange(height, 44, 48);

        surface.Window.Close();
    }

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Avalonia.Application.Current!.Resources[key]!).Color;

    [Fact]
    [Trait("Category", "Gate")]
    public void TheChecklistSourceDrawsOnlyInTheNewTokens()
    {
        var source = File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), "src", "D47.App", "Panel", "ChecklistPage.cs"));

        KitTokens.AssertDrawsOnlyInTheNewTokens(source, new KitTokenRules(true, false, true));
    }
}
