using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

// System.IO is implicitly imported and System.IO.Path is not the one meant here — the same aliasing Glyphs.cs
// carries, for the same reason.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Tests;

/// <summary>A clickable word or glyph carries the theme accent at rest.</summary>
public class ClickableMarksCarryTheAccentTests
{
    private static (PanelView Panel, Window Window) Shown()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(themeId: null);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 1000, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    private static string? Resolved(Window window, string key) =>
        ((IBrush?)window.FindResource(key))?.ToString();

    private static Path Glyph(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Path>().Single(mark => mark.Name == name);

    [AvaloniaFact]
    public void TheHelpMarkIsAccentWithNobodyPointingAtIt()
    {
        var (panel, window) = Shown();

        Assert.Equal(Resolved(window, ThemeManager.AccentKey), Glyph(panel, "HelpGlyph").Stroke?.ToString());
        Assert.NotEqual(Resolved(window, ThemeManager.TextMutedKey), Glyph(panel, "HelpGlyph").Stroke?.ToString());

        window.Close();
    }

    /// <summary>And it follows a theme switch, which is the regression the removed handlers risked.</summary>
    [AvaloniaFact]
    public void AndItRepaintsWhenTheThemeChanges()
    {
        var (panel, window) = Shown();
        var themes = new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance);

        // Elite's accent, which is what the default theme paints and what the mark starts as.
        var elite = Glyph(panel, "HelpGlyph").Stroke?.ToString();

        Assert.Equal(Resolved(window, ThemeManager.AccentKey), elite);

        foreach (var theme in new[] { ThemeCatalog.Light, ThemeCatalog.Dark })
        {
            themes.Apply(theme);
            Dispatcher.UIThread.RunJobs();

            var now = Glyph(panel, "HelpGlyph").Stroke?.ToString();

            Assert.Equal(Resolved(window, ThemeManager.AccentKey), now);

            // **And it genuinely moved.** Light is #0A64C8 and dark is #4C8DFF against Elite's #FF7100, so a
            // mark that had been painted once and left would fail here rather than passing because nothing
            // was ever different.
            Assert.NotEqual(elite, now);
        }

        window.Close();
    }

    /// <summary>The hover pair is gone rather than replaced.</summary>
    [AvaloniaFact]
    public void NothingSwapsTheStrokeOnTheWayInAndOut()
    {
        var source = File.ReadAllText(System.IO.Path.Combine(RepositoryRoot(), "src/D47.App/Panel/PanelView.axaml.cs"));

        Assert.DoesNotContain("HelpGlyph.Stroke =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnHelpPointerEntered", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnHelpPointerExited", source, StringComparison.Ordinal);
    }

    /// <summary>Every bare glyph on the panel, not just the one that was reported.</summary>
    [AvaloniaFact]
    public void TheTranscriptBarsMarksAreAccentToo()
    {
        var (panel, window) = Shown();

        panel.EnableSearch();
        panel.Tab = D47.Core.Interface.PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        var copy = panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "CopyButton");

        Assert.Equal(
            Resolved(window, ThemeManager.AccentKey),
            Assert.IsType<Path>(copy.Content).Stroke?.ToString());

        window.Close();
    }

    /// <summary>
    /// And the checklist's Add mark, which was the colour of the words around it — so the one control
    /// on that bar with no chrome of its own was also the one not saying it was a control.
    /// </summary>
    [AvaloniaFact]
    public void TheChecklistsAddMarkIsAccent()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(themeId: null);

        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-accent-tests"));
        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(System.IO.Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                System.IO.Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1200, Height = 700 };

        window.Show();
        panel.Tab = D47.Core.Interface.PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        var accent = Resolved(window, ThemeManager.AccentKey);
        var muted = Resolved(window, ThemeManager.TextMutedKey);

        // By its accessible name, which Glyphs.Mark sets from the same string as the tooltip — and which is
        // the only text a glyph-only button has left to be found by.
        var add = panel.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Add a line");

        Assert.Equal(accent, Assert.IsType<Path>(add.Content).Stroke?.ToString());
        Assert.NotEqual(muted, Assert.IsType<Path>(add.Content).Stroke?.ToString());

        window.Close();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
