using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commanders, Crew and the two Phrases tables take the COLUMNS control and keep what it stores (#2525).</summary>
[Trait("Category", "Integration")]
public sealed class TheCommanderCrewAndPhraseTablesAreArrangedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private static ViewStateStore Views() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static void Press(Visual root, string name)
    {
        var button = root.GetVisualDescendants().OfType<Button>().First(control => control.Name == name);
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string[] Keys(Visual root, string id) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == id && grid.IsHeader).Layout.Columns.Select(column => column.Key)];

    private static void Add(Visual root, string id, string key)
    {
        if (root.GetVisualDescendants().OfType<CheckBox>().All(box => box.Name != ColumnArranger.NameOf(id, $"Show.{key}")))
        {
            Press(root, ColumnArranger.NameOf(id, "Add"));
        }

        root.GetVisualDescendants().OfType<CheckBox>().First(box => box.Name == ColumnArranger.NameOf(id, $"Show.{key}")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static CommanderRoster Roster() =>
        new(
            () => new Dictionary<string, CommanderSighting>
            {
                ["F1"] = new("F1", "John Deparagon", Now, "Sol", "Anaconda"),
                ["F2"] = new("F2", "Jameson", Now.AddDays(-3), null, "Sidewinder"),
            },
            () => 2,
            () => "F1",
            () => false,
            _ => { });

    private static GameStateStore Store(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static GameStateStore CrewStore() =>
        Store(
            """{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-10-05T09:01:00Z","event":"CrewHire","Name":"Samira Voss","CrewID":1,"CombatRank":6}""");

    private static (CapabilityRegistry Registry, LearnedPhrasesStore Learned, GameStateStore Store) Phrases()
    {
        var root = TestSurface.MemoryFolder("d47-phrases-arranged");
        var learned = new LearnedPhrasesStore(Path.Combine(root, "phrases.json"), new MemoryFileSystem(), NullLogger<LearnedPhrasesStore>.Instance);
        learned.Learn("F1", "lights on", "new phrase", Now);
        var store = Store("""{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");

        CapabilityRegistry? registry = null;
        registry = CapabilityRegistry.Build(
        [
            LearnedPhrasesCapability.Create(
                learned,
                () => store.Active?.Identity.FrontierId ?? string.Empty,
                () => PhraseBook.From(registry!, [])),
            InterfaceCapability.Create(),
            .. ActionCapabilities.All(ActionSurface.Inert),
        ]);

        return (registry, learned, store);
    }

    [AvaloniaFact]
    public void TheCommandersTableKeepsTheSwitchLastAndASortFollowsItsColumn()
    {
        var views = Views();
        var (window, page) = PageHost.Open(() => new CommandersPage(Roster(), null, null, new ColumnArrangementMemory(views)));

        Assert.Equal(["Name", "Ship", "LastLocation", "LastDetected", "Switch"], Keys(page, CommandersPage.TableId));

        Press(page, "CommandersHead.SHIP");
        Press(page, ColumnArranger.NameOf(CommandersPage.TableId, "Tile"));
        Press(page, "CommandersHead.Choose.Ship");
        Press(page, ColumnArranger.NameOf(CommandersPage.TableId, "MoveRight"));
        Press(page, ColumnArranger.NameOf(CommandersPage.TableId, "Done"));

        var arranged = Keys(page, CommandersPage.TableId);
        Assert.Equal(["Name", "LastLocation", "Ship", "LastDetected", "Switch"], arranged);
        Assert.Contains(
            page.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text is { } said && said.StartsWith("SHIP", StringComparison.Ordinal) && said.EndsWith('▲'));
        window.Close();

        var (againWindow, again) = PageHost.Open(() => new CommandersPage(Roster(), null, null, new ColumnArrangementMemory(views)));
        Assert.Equal(arranged, Keys(again, CommandersPage.TableId));

        Press(again, ColumnArranger.NameOf(CommandersPage.TableId, "Tile"));
        Press(again, ColumnArranger.NameOf(CommandersPage.TableId, "Default"));
        Assert.Equal(["Name", "Ship", "LastLocation", "LastDetected", "Switch"], Keys(again, CommandersPage.TableId));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void TheCrewTableOffersShipChangedAndKeepsWhereItIsPut()
    {
        var views = Views();
        var store = CrewStore();
        var (window, page) = PageHost.Open(() => new CrewPage(() => store.Active, null, null, new ColumnArrangementMemory(views)));

        Assert.Equal(["Pilot", "CombatRank", "FighterBay", "PostedTo"], Keys(page, CrewPage.TableId));

        Press(page, ColumnArranger.NameOf(CrewPage.TableId, "Tile"));
        Add(page, CrewPage.TableId, "HullLeft");
        Press(page, "CrewHead.Choose.PostedTo");
        Press(page, ColumnArranger.NameOf(CrewPage.TableId, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(CrewPage.TableId, "Done"));

        var arranged = Keys(page, CrewPage.TableId);
        Assert.Equal(["Pilot", "CombatRank", "PostedTo", "FighterBay", "HullLeft"], arranged);
        Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "No");
        window.Close();

        var (againWindow, again) = PageHost.Open(() => new CrewPage(() => store.Active, null, null, new ColumnArrangementMemory(views)));
        Assert.Equal(arranged, Keys(again, CrewPage.TableId));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void YourPhrasesOffersLearnedAndTheArrowAndForgetStayWithTheirColumns()
    {
        var views = Views();
        var (registry, learned, store) = Phrases();
        var id = PhrasesPage.OwnTable;
        var (window, page) = PageHost.Open(() => new PhrasesPage(registry, learned, () => store.Active, null, null, new ColumnArrangementMemory(views)));

        Assert.Equal(["Phrase", "Arrow", "Meaning", "Forget"], Keys(page, id));

        Press(page, ColumnArranger.NameOf(id, "Tile"));
        Add(page, id, "Learned");
        Press(page, "OwnPhrasesHead.Choose.Meaning");
        Press(page, ColumnArranger.NameOf(id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(id, "Done"));

        var arranged = Keys(page, id);
        Assert.Equal(["Meaning", "Phrase", "Arrow", "Learned", "Forget"], arranged);
        Assert.Contains(page.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "2026-10-05");
        window.Close();

        var (againWindow, again) = PageHost.Open(() => new PhrasesPage(registry, learned, () => store.Active, null, null, new ColumnArrangementMemory(views)));
        Assert.Equal(arranged, Keys(again, id));

        Press(again, ColumnArranger.NameOf(id, "Tile"));
        Press(again, ColumnArranger.NameOf(id, "Default"));
        Assert.Equal(["Phrase", "Arrow", "Meaning", "Forget"], Keys(again, id));
        againWindow.Close();
    }

    [AvaloniaFact]
    public void TheBuiltInPhrasesTableMovesAColumn()
    {
        var views = Views();
        var (registry, learned, store) = Phrases();
        var id = PhrasesPage.BuiltInTable;
        var (window, page) = PageHost.Open(() => new PhrasesPage(registry, learned, () => store.Active, null, null, new ColumnArrangementMemory(views)));

        Assert.Equal(["Phrase", "Does"], Keys(page, id));

        Press(page, ColumnArranger.NameOf(id, "Tile"));
        Press(page, "BuiltInPhrasesHead.Choose.Does");
        Press(page, ColumnArranger.NameOf(id, "MoveLeft"));
        Press(page, ColumnArranger.NameOf(id, "Done"));

        Assert.Equal(["Does", "Phrase"], Keys(page, id));
        window.Close();
    }
}
