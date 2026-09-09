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

/// <summary>"Nothing on your list matches that." never says what "that" was — survivable on the full
/// panel, where the query box and the scope button sit beside it, and not in mini, where neither does
/// (#94).</summary>
public class AnEmptyChecklistNamesItsFilterTests
{
    private static ChecklistService Checklists()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-empty-checklist-message"));
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);
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

    private static ChecklistItem Note(string text) => new()
    {
        Key = text.Replace(' ', '-').ToLowerInvariant(),
        Scope = ChecklistScope.Universal,
        Kind = ChecklistItemKind.Authored,
        Source = ChecklistSource.Commander,
        Text = text,
    };

    private static IReadOnlyList<string> Lines(Avalonia.Visual panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)];

    private static IReadOnlyList<string> Buttons(Avalonia.Visual panel) =>
        [.. panel.GetVisualDescendants()
            .OfType<Button>()
            .Select(button => button.Content?.ToString() ?? string.Empty)];

    [AvaloniaFact]
    public void AnUnfilteredEmptyListStillReadsNothingHereYet()
    {
        var (window, panel) = Open(Checklists());

        Assert.Contains(Lines(panel), text => text.StartsWith("Nothing here yet", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void AQueryThatMatchesNothingNamesTheQuery()
    {
        var checklists = Checklists();
        checklists.List.Save(
            [ChecklistDocument.For(string.Empty, "Jameson") with { Items = [Note("buy limpets")] }]);

        var (window, panel) = Open(checklists);

        checklists.Search("thargoid sensors");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Lines(panel), text => text == "Nothing on your list matches 'thargoid sensors'.");

        window.Close();
    }

    [AvaloniaFact]
    public void AScopeThatMatchesNothingNamesTheScopesWord()
    {
        var checklists = Checklists();
        checklists.List.Save(
            [ChecklistDocument.For(string.Empty, "Jameson") with { Items = [Note("buy limpets")] }]);

        var (window, panel) = Open(checklists);

        checklists.Choose("complete");
        Dispatcher.UIThread.RunJobs();

        var scopeWord = Buttons(panel).Single(text => text.StartsWith("Showing ", StringComparison.Ordinal))
            ["Showing ".Length..];

        Assert.Contains(Lines(panel), text => text == $"Nothing on your list is in {scopeWord}.");

        window.Close();
    }

    [AvaloniaFact]
    public void AQueryAndAScopeTogetherNameBoth()
    {
        var checklists = Checklists();
        checklists.List.Save(
            [ChecklistDocument.For(string.Empty, "Jameson") with { Items = [Note("buy limpets")] }]);

        var (window, panel) = Open(checklists);

        checklists.Choose("complete");
        checklists.Search("limpets");
        Dispatcher.UIThread.RunJobs();

        var scopeWord = Buttons(panel).Single(text => text.StartsWith("Showing ", StringComparison.Ordinal))
            ["Showing ".Length..];

        Assert.Contains(Lines(panel), text => text == $"Nothing in {scopeWord} matches 'limpets'.");

        window.Close();
    }

    /// <summary>Mini hides the search box and keeps filtering by the query it set: the empty list still
    /// names both criteria, through the mini surface rather than the full one.</summary>
    [AvaloniaFact]
    public void MiniNamesBothCriteriaWithNoBoxOnScreenToReadThemFrom()
    {
        var checklists = Checklists();
        checklists.List.Save(
            [ChecklistDocument.For(string.Empty, "Jameson") with { Items = [Note("buy limpets")] }]);

        var (window, panel) = Open(checklists);

        checklists.Choose("complete");
        checklists.Search("limpets");
        Dispatcher.UIThread.RunJobs();

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        var scopeWord = Buttons(panel).Single(text => text.StartsWith("Showing ", StringComparison.Ordinal))
            ["Showing ".Length..];

        Assert.Contains(Lines(panel), text => text == $"Nothing in {scopeWord} matches 'limpets'.");

        window.Close();
    }

    /// <summary>Mini has no search box to clear the query with, so the empty state grows its own way out.</summary>
    [AvaloniaFact]
    public void MiniOffersAButtonThatClearsTheFilter()
    {
        var checklists = Checklists();
        checklists.List.Save(
            [ChecklistDocument.For(string.Empty, "Jameson") with { Items = [Note("buy limpets")] }]);

        var (window, panel) = Open(checklists);

        checklists.Search("thargoid sensors");
        Dispatcher.UIThread.RunJobs();

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        var clear = panel.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content?.ToString() == "Clear filter");

        clear.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, checklists.Query);
        Assert.Equal(ChecklistService.Everything, checklists.Filter);
        Assert.Contains(Lines(panel), text => text.Contains("buy limpets", StringComparison.Ordinal));

        window.Close();
    }
}
