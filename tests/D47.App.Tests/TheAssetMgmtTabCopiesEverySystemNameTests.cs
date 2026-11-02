using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The ship cards and rows, Compare and Stored modules draw a copy glyph for every system name.</summary>
[Trait("Category", "Integration")]
public class TheAssetMgmtTabCopiesEverySystemNameTests
{
    private const string Commander = """{"timestamp":"2026-10-24T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private static List<string> Copies(Avalonia.Visual root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Select(CopyGlyph.GetCopies).OfType<string>()];

    private static T Shown<T>(T control)
        where T : Control
    {
        new Window { Content = control, Width = 1100, Height = 800 }.Show();
        Dispatcher.UIThread.RunJobs();

        return control;
    }

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

    private static Task<bool> Ok(string text) => Task.FromResult(true);

    [AvaloniaFact]
    public void AShipCardWithASystemEndsItsWhereLineInAGlyphThatCopiesIt()
    {
        var written = new List<string>();
        var pressed = false;

        var card = Shown(LoadoutPages.Card(
            "Reaper (Cobra Mk V)",
            "Cobra Mk V\nParked in Laksak\n2 planned",
            marked: true,
            () => pressed = true,
            LoadoutStanding.Owned,
            system: "Laksak",
            copy: text => { written.Add(text); return Task.FromResult(true); }));

        var glyph = card.GetVisualDescendants().OfType<Button>().Single(button => CopyGlyph.GetCopies(button) == "Laksak");
        glyph.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Laksak"], written);
        Assert.False(pressed);
    }

    [AvaloniaFact]
    public void AShipCardWithNoSystemOrNoCopyDelegateDrawsNoGlyph()
    {
        var noSystem = Shown(LoadoutPages.Card("Reaper", "Cobra Mk V\nin transit", marked: false, () => { }, LoadoutStanding.Owned, copy: Ok));
        var noCopy = Shown(LoadoutPages.Card("Reaper", "Cobra Mk V\nParked in Laksak", marked: false, () => { }, LoadoutStanding.Owned, system: "Laksak"));

        Assert.Empty(Copies(noSystem));
        Assert.Empty(Copies(noCopy));
    }

    [AvaloniaFact]
    public void AShipRowWithASystemEndsInAGlyphThatCopiesItAndDoesNotOpenTheShip()
    {
        var written = new List<string>();
        var pressed = false;

        var row = Shown(LoadoutPages.Row(
            "Reaper (Cobra Mk V)",
            "Parked in Laksak",
            marked: false,
            () => pressed = true,
            system: "Laksak",
            copy: text => { written.Add(text); return Task.FromResult(true); }));

        row.GetVisualDescendants().OfType<Button>().Single(button => CopyGlyph.GetCopies(button) == "Laksak")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Laksak"], written);
        Assert.False(pressed);
        Assert.Empty(Copies(Shown(LoadoutPages.Row("Reaper", "in transit", marked: false, () => { }, copy: Ok))));
    }

    private static ShipsMode Mode(params string[] journal)
    {
        var root = TestSurface.MemoryFolder("d47-asset-mgmt-copy-tests");
        var state = Store([Commander, .. journal]);

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => state.Active);
        var ships = TestSurface.ShipPlans(checklists, root, () => state.Active);

        return new ShipsMode(ships, checklists, () => state.Active);
    }

    [AvaloniaFact]
    public void AParkedShipsRowNamesItsSystemAndAShipInTransitNamesNone()
    {
        var mode = Mode(
            """{"timestamp":"2026-10-24T09:01:00Z","event":"StoredShips","StarSystem":"Sol","StationName":"Galileo","ShipsHere":[],"ShipsRemote":[{"ShipID":12,"ShipType":"cobramkv","Name":"Reaper","Value":1,"StarSystem":"Laksak"},{"ShipID":13,"ShipType":"asp","Name":"Drifter","Value":1,"InTransit":true}]}""");

        var rows = mode.Items();

        Assert.Equal("Laksak", rows.Single(row => row.Word == "Reaper").AsideSystem);
        Assert.Null(rows.Single(row => row.Word == "Drifter").AsideSystem);
    }

    private const string Remote =
        """{"timestamp":"2026-10-24T09:01:00Z","event":"StoredShips","StarSystem":"Sol","StationName":"Galileo","ShipsHere":[],"ShipsRemote":[{"ShipID":12,"ShipType":"cobramkv","Name":"Reaper","Value":1,"StarSystem":"Laksak"},{"ShipID":13,"ShipType":"asp","Name":"Drifter","Value":1,"InTransit":true}]}""";

    [AvaloniaFact]
    public void TheCompareWhereColumnCopiesEachKnownSystem()
    {
        var state = Store(Commander, Remote);
        var written = new List<string>();

        var page = Shown(new ComparePage(() => state.Active, copy: text => { written.Add(text); return Task.FromResult(true); }));

        Assert.Equal(["Laksak"], Copies(page));

        page.GetVisualDescendants().OfType<Button>().Single(button => CopyGlyph.GetCopies(button) == "Laksak")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Laksak"], written);
    }

    [AvaloniaFact]
    public void TheCompareWhereColumnWithNoCopyDelegateDrawsNoGlyph()
    {
        var state = Store(Commander, Remote);

        Assert.Empty(Copies(Shown(new ComparePage(() => state.Active))));
    }

    private const string Stored =
        """{"timestamp":"2026-10-24T09:02:00Z","event":"StoredModules","MarketID":1,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[{"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":1,"Hot":false},{"Name":"$int_powerdistributor_size5_class5_name;","Name_Localised":"Power Distributor","StorageSlot":2,"StarSystem":"Deciat","MarketID":2,"TransferCost":1,"TransferTime":840,"BuyPrice":1,"Hot":false},{"Name":"$int_cargorack_size7_class1_name;","Name_Localised":"Cargo Rack","StorageSlot":3,"TransferCost":1,"TransferTime":840,"BuyPrice":1,"Hot":false,"InTransit":true}]}""";

    [AvaloniaFact]
    public void EachStoredModulesSystemHeadingCopiesItsSystemAndInTransitHasNone()
    {
        var state = Store(
            Commander,
            """{"timestamp":"2026-10-24T09:00:01Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"StarPos":[55.7,17.6,27.2],"Docked":true,"StationName":"Jameson Memorial"}""",
            Stored);

        var page = Shown(new StoredModulesPage(() => state.Active, copy: Ok));
        var copies = Copies(page);

        Assert.Contains("Shinrarta Dezhra", copies);
        Assert.Contains("Deciat", copies);
        Assert.DoesNotContain(StoredModulesPage.InTransit, copies);

        Assert.Empty(Copies(Shown(new StoredModulesPage(() => state.Active))));
    }
}
