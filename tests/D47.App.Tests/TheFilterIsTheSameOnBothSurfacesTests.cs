using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Checklist filter was applied in the Windows app and the headset, a foot away, went on drawing
/// the unfiltered list.</summary>
public class TheFilterIsTheSameOnBothSurfacesTests
{
    private static ChecklistService Checklists(string root, Action<ChecklistView>? remember = null)
    {
        var paths = new D47.Core.AppPaths(root);
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null,
            remember);
    }

    private static (Window Window, PanelView Panel) Open(ChecklistService checklists)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static IReadOnlyList<string> Lines(PanelView panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)];

    private static ChecklistItem Note(string text) => new()
    {
        Key = text.Replace(' ', '-').ToLowerInvariant(),
        Scope = ChecklistScope.Universal,
        Kind = ChecklistItemKind.Authored,
        Source = ChecklistSource.Commander,
        Text = text,
    };

    [AvaloniaFact]
    public void AFilterChosenOnOneSurfaceNarrowsTheOther()
    {
        var checklists = Checklists(TempFolders.Create("d47-shared-filter"));

        checklists.List.Save(
        [
            ChecklistDocument.For(string.Empty, "Jameson") with
            {
                Items = [Note("buy limpets"), Note("sell the cargo") with { State = ChecklistState.Done }],
            },
        ]);

        var (windowOne, deskPanel) = Open(checklists);
        var (windowTwo, headsetPanel) = Open(checklists);

        Assert.Contains(Lines(deskPanel), text => text.Contains("buy limpets", StringComparison.Ordinal));
        Assert.Contains(Lines(headsetPanel), text => text.Contains("sell the cargo", StringComparison.Ordinal));

        // What the Commander did at the desk. Settled first, and the test is worthless without it. A
        // rebuild queued by the opening layout was still pending, and it redrew both surfaces off the new
        // filter whether or not anything had been told about the change — so this passed with the
        // notification deliberately removed.
        Dispatcher.UIThread.RunJobs();

        checklists.Choose("open");
        Dispatcher.UIThread.RunJobs();

        // And what the headset shows without anybody touching it.
        Assert.Contains(Lines(headsetPanel), text => text.Contains("buy limpets", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Lines(headsetPanel),
            text => text.Contains("sell the cargo", StringComparison.Ordinal));

        windowOne.Close();
        windowTwo.Close();
    }

    /// <summary>
    /// The search box travels with the filter, or the same report arrives again about the box instead
    /// of the chooser.
    /// </summary>
    [AvaloniaFact]
    public void ASearchTypedOnOneSurfaceNarrowsTheOther()
    {
        var checklists = Checklists(TempFolders.Create("d47-shared-filter"));

        checklists.List.Save(
        [
            ChecklistDocument.For(string.Empty, "Jameson") with
            {
                Items = [Note("buy limpets"), Note("sell the cargo")],
            },
        ]);

        var (windowOne, _) = Open(checklists);
        var (windowTwo, headsetPanel) = Open(checklists);

        Dispatcher.UIThread.RunJobs();

        checklists.Search("limpets");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Lines(headsetPanel), text => text.Contains("buy limpets", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Lines(headsetPanel),
            text => text.Contains("sell the cargo", StringComparison.Ordinal));

        windowOne.Close();
        windowTwo.Close();
    }

    /// <summary>
    /// The filter is remembered and the search is not, which is the line between a preference and where
    /// somebody is looking this minute.
    /// </summary>
    [AvaloniaFact]
    public void TheFilterIsWrittenDownAndTheSearchIsNot()
    {
        var written = new List<ChecklistView>();
        var checklists = Checklists(TempFolders.Create("d47-shared-filter"), written.Add);

        checklists.Choose("open");
        checklists.Search("limpets");

        Assert.Equal(["open"], written.Select(view => view.Filter));

        // And a later run takes it up without writing it back or redrawing anything.
        var next = Checklists(TempFolders.Create("d47-shared-filter"), written.Add);

        next.Restore(new ChecklistView("open", IncludePartialGrades: false));

        Assert.Equal("open", next.Filter);
        Assert.Single(written);
    }

    /// <summary>The headset can see what it is under.</summary>
    [AvaloniaFact]
    public void TheMiniPanelStillSaysWhatTheListIsUnder()
    {
        var checklists = Checklists(TempFolders.Create("d47-shared-filter"));

        checklists.List.Save(
        [
            ChecklistDocument.For(string.Empty, "Jameson") with { Items = [Note("buy limpets")] },
        ]);

        var (window, panel) = Open(checklists);

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        checklists.Choose("open");
        Dispatcher.UIThread.RunJobs();

        var buttons = panel.GetVisualDescendants()
            .OfType<Button>()
            .Select(button => button.Content?.ToString() ?? string.Empty)
            .ToList();

        Assert.Contains(buttons, text => text.StartsWith("Showing ", StringComparison.Ordinal));

        window.Close();
    }
}
