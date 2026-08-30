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

// System.IO is implicitly imported and System.IO.Path is not the one meant here — the same
// aliasing Glyphs.cs carries, for the same reason.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Tests;

/// <summary>
/// A clickable word or glyph carries the theme accent at rest
/// (<a href="https://github.com/dseelinger/d47/issues/208">#208</a>).
/// <para>
/// <b>The Help mark was the report and not the finding.</b> It was muted until pointed at, and the
/// rule turned out not to be applied consistently anywhere: the mode toggle was accent, Help was
/// muted-then-accent, both settings resets were muted, Add was the colour of the words around it,
/// and Copy was muted. Five bare glyphs, four different answers.
/// </para>
/// <para>
/// <b>The boundary is what stops "clickable things are orange" repainting the app.</b> In scope is
/// a bare word or glyph on a transparent background whose <em>only</em> affordance is that it can
/// be pressed — nothing about its shape says press me, so the colour has to. Out of scope are
/// controls that already say it by shape and carry their own chrome: checkboxes and their labels,
/// the tab strip, scrollbar parts, and anything using the accent as a <em>background</em>, where an
/// accent foreground would vanish into it.
/// </para>
/// <para>
/// Asserted against the real panel rather than off a probe, because the claim is about what a
/// Commander sees — and against the resolved brush rather than a literal, because orange is the
/// Elite palette's answer to the accent key and not the value being written.
/// </para>
/// </summary>
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

    /// <summary>
    /// <b>The report itself.</b> No pointer has been anywhere near it, and it is already accent.
    /// </summary>
    [AvaloniaFact]
    public void TheHelpMarkIsAccentWithNobodyPointingAtIt()
    {
        var (panel, window) = Shown();

        Assert.Equal(Resolved(window, ThemeManager.AccentKey), Glyph(panel, "HelpGlyph").Stroke?.ToString());
        Assert.NotEqual(Resolved(window, ThemeManager.TextMutedKey), Glyph(panel, "HelpGlyph").Stroke?.ToString());

        window.Close();
    }

    /// <summary>
    /// <b>And it follows a theme switch, which is the regression the removed handlers risked.</b>
    /// They read a brush with <c>FindResource</c> — once, at the moment of the hover — and assigned
    /// it to the property, so a theme changed afterwards left the mark painted in the old theme's
    /// colour with a local value outranking the markup that would have repainted it. A
    /// <c>DynamicResource</c> is the only mechanism on this stroke now, and this is what says so.
    /// </summary>
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

            // **And it genuinely moved.** Light is #0A64C8 and dark is #4C8DFF against Elite's
            // #FF7100, so a mark that had been painted once and left would fail here rather than
            // passing because nothing was ever different.
            Assert.NotEqual(elite, now);
        }

        window.Close();
    }

    /// <summary>
    /// The hover pair is gone rather than replaced. The colour was saying two things at once —
    /// "this can be pressed" and "you are pointing at it" — and the resting state spends the first
    /// of them, so the second has to be said some other way or not at all.
    /// </summary>
    [AvaloniaFact]
    public void NothingSwapsTheStrokeOnTheWayInAndOut()
    {
        var source = File.ReadAllText(System.IO.Path.Combine(RepositoryRoot(), "src/D47.App/Panel/PanelView.axaml.cs"));

        Assert.DoesNotContain("HelpGlyph.Stroke =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnHelpPointerEntered", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OnHelpPointerExited", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Every bare glyph on the panel, not just the one that was reported.</b> Copy sits on the
    /// transcript's own bar with no chrome of its own, exactly as Help does; it was muted, and the
    /// issue's table did not list it. Leaving it would have reproduced the finding the report is
    /// actually about — that the rule is applied nowhere consistently.
    /// </summary>
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
    /// And the checklist's Add mark, which was the colour of the words around it — so the one
    /// control on that bar with no chrome of its own was also the one not saying it was a control.
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

        // By its accessible name, which Glyphs.Mark sets from the same string as the tooltip —
        // and which is the only text a glyph-only button has left to be found by.
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
