using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Each Discoveries row has a Search tile that runs the entry's body search, beside the row that still opens the entry page (#1823).</summary>
public sealed class ASearchTileOnADiscoveriesRowRunsTheBodySearchTests
{
    private static readonly CodexTable Table = CodexTable.Parse(
        "1\tAlpha tree\tBiological and Geological\tOrganic structures\t18:50\n"
        + "2\tBeta tree\tBiological and Geological\tOrganic structures\t18:30\n");

    private static (Window Window, CodexDiscoveriesPage Page) Shown(BodySearchLink? link, int? regionId = 18, CodexTable? table = null)
    {
        var state = new CommanderGameState(new CommanderIdentity("F735466", "Test Commander"));
        var page = new CodexDiscoveriesPage(() => state, () => HistoryState.Done, regionId, table ?? Table, bodySearch: () => link);
        var window = new Window { Content = page, Width = 1280, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static Button? Find(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().SingleOrDefault(button => button.Name == name);

    [AvaloniaFact]
    public void PressingSearchRunsTheEntrySearchAndLeavesTheRowToOpenThePage()
    {
        var asked = new List<IReadOnlyDictionary<string, string>>();
        var (window, page) = Shown(new BodySearchLink(asked.Add));

        var tile = Assert.IsType<Button>(Find(page, "CodexSearch.2"));
        Assert.True(tile.IsEnabled);
        Assert.Equal(96, tile.Width);
        tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var sent = Assert.Single(asked);
        Assert.Equal("Beta tree", sent["codex_entry"]);
        Assert.Equal(GalacticRegions.NameOf(18), sent["region"]);
        Assert.False(sent.ContainsKey("max_distance"));
        Assert.NotNull(Find(page, "CodexMissing.2"));

        window.Close();
    }

    [AvaloniaFact]
    public void ARegionD47CannotNameSendsTheEntryAlone()
    {
        var asked = new List<IReadOnlyDictionary<string, string>>();
        var (window, page) = Shown(
            new BodySearchLink(asked.Add),
            regionId: 99,
            table: CodexTable.Parse("2\tBeta tree\tBiological and Geological\tOrganic structures\t99:30\n"));

        Assert.IsType<Button>(Find(page, "CodexSearch.2")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var sent = Assert.Single(asked);
        Assert.Equal(["codex_entry"], sent.Keys);

        window.Close();
    }

    [AvaloniaFact]
    public void WithoutASearchLinkTheRowHasNoTile()
    {
        var (window, page) = Shown(null);

        Assert.Null(Find(page, "CodexSearch.1"));
        Assert.NotNull(Find(page, "CodexMissing.1"));

        window.Close();
    }
}
