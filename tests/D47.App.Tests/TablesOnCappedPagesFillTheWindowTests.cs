using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Activities;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A table on Crew, Stored modules, Phrases, Commanders and Activities fills a 1600px window instead of stopping at the page's old cap.</summary>
public class TablesOnCappedPagesFillTheWindowTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private const string Commander = """{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

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

    private static Window Show(Control page)
    {
        var look = AppLook.Put();
        var window = new Window
        {
            Content = page,
            Width = 1600,
            Height = 1400,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void FillsPast(Window window, string id, double oldCap)
    {
        var header = window.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == id && grid.IsHeader);

        Assert.True(header.Bounds.Width > oldCap, $"{id} is {header.Bounds.Width} wide; its old cap was {oldCap}");
        window.Close();
    }

    [AvaloniaFact]
    public void TheCrewTableFillsTheWindow()
    {
        var store = Store(Commander, """{"timestamp":"2026-10-05T09:01:00Z","event":"CrewHire","Name":"Samira Voss","CrewID":1,"CombatRank":6}""");

        FillsPast(Show(new CrewPage(() => store.Active)), CrewPage.TableId, 1000);
    }

    [AvaloniaFact]
    public void TheStoredModulesTablesFillTheWindow()
    {
        var store = Store(
            Commander,
            """{"timestamp":"2026-10-04T09:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"StarPos":[55.7,17.6,27.2],"Docked":true,"StationName":"Jameson Memorial"}""",
            """{"timestamp":"2026-10-04T09:01:00Z","event":"StoredModules","MarketID":128666762,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[{"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":16179531,"Hot":false}]}""");

        FillsPast(Show(new StoredModulesPage(() => store.Active)), StoredModulesPage.TableId, 1000);
    }

    [AvaloniaFact]
    public void ThePhrasesTablesFillTheWindow()
    {
        var root = TestSurface.MemoryFolder("d47-phrases-fill");
        var learned = new LearnedPhrasesStore(Path.Combine(root, "phrases.json"), new MemoryFileSystem(), NullLogger<LearnedPhrasesStore>.Instance);
        learned.Learn("F1", "lights on", "new phrase", At);
        var store = Store(Commander);

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

        var window = Show(new PhrasesPage(registry, learned, () => store.Active));

        Assert.True(window.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == PhrasesPage.OwnTable && grid.IsHeader).Bounds.Width > PhrasesPage.Cap);
        FillsPast(window, PhrasesPage.BuiltInTable, PhrasesPage.Cap);
    }

    [AvaloniaFact]
    public void TheCommandersTableFillsTheWindow()
    {
        var found = new Dictionary<string, CommanderSighting>
        {
            ["F1"] = new("F1", "John Deparagon", At, "Sol", "Anaconda"),
            ["F2"] = new("F2", "Jameson", At.AddDays(-3), "Alpha Centauri", "Sidewinder"),
        };
        var roster = new CommanderRoster(() => found, () => 2, () => "F1", () => false, _ => { });

        FillsPast(Show(new CommandersPage(roster)), CommandersPage.TableId, CommandersPage.Cap);
    }

    [AvaloniaFact]
    public void TheActivitiesTableFillsTheWindow()
    {
        var ledger = new ActivityLedger(new MemoryFileSystem(), NullLogger.Instance);

        FillsPast(Show(new ActivitiesPage(ledger, () => "F1", () => At)), ActivitiesPage.TableId, ActivitiesPage.Cap);
    }

    [AvaloniaFact]
    public void TheActivitiesTableInStatisticsFillsTheWindow()
    {
        var ledger = new ActivityLedger(new MemoryFileSystem(), NullLogger.Instance);
        var store = Store(Commander);
        var clock = new JournalClock(() => store.Active?.Session.LastEventAt);
        var page = new StatisticsPage(() => store.Active, clock, () => { }, ledger, opening: StatisticsPage.ActivitiesKey);

        FillsPast(Show(page), ActivitiesPage.TableId, StatisticsPage.Cap);
    }
}
