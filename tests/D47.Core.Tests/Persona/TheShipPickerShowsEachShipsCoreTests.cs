using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

public class TheShipPickerShowsEachShipsCoreTests
{
    [Fact]
    public void ABoundShipShowsItsCoreAndAnUnboundShipShowsNoCore()
    {
        var (ship, _, settings) = Rows(bound: (53, "quartermaster"));

        var bound = ship.StatusFor("53", settings);
        var unbound = ship.StatusFor("99", settings);

        Assert.Equal(ChoiceTone.Accent, bound!.Tone);
        Assert.Equal(PersonaCatalog.Resolve("quartermaster").Name, bound.Text);
        Assert.Equal(new ChoiceStatus("NO CORE", ChoiceTone.Grey), unbound);
    }

    [Fact]
    public void TheShipAboardIsMarkedInCyan()
    {
        var (ship, _, settings) = Rows(bound: (7, "quartermaster"));

        var aboard = ship.StatusFor("7", settings);

        Assert.Equal(ChoiceTone.Cyan, aboard!.Tone);
        Assert.EndsWith("· aboard", aboard.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoShipPickedTheShipRowShowsTheShipAboard()
    {
        var (ship, _, settings) = Rows();

        Assert.Equal("7", ship.Binding!.Read(settings));
    }

    [Fact]
    public void APickedShipIsKeptOverTheShipAboard()
    {
        var (ship, _, settings) = Rows(picked: 53);

        Assert.Equal("53", ship.Binding!.Read(settings));
    }

    [Fact]
    public void ChoosingACoreWithNoShipPickedBindsTheShipAboard()
    {
        var (_, core, settings) = Rows(out var ships);

        core.Binding!.Write!(settings, "quartermaster");

        Assert.Equal("quartermaster", ships.For(7)!.Core);
        Assert.Null(ships.For(53));
    }

    [Fact]
    public void TheCoreRowIsLabelledWithTheShipTheShipRowShows()
    {
        var (_, core, aboard) = Rows();
        var (_, _, picked) = Rows(picked: 53);

        Assert.Equal("Core for Bad Idea, a Python", core.CaptionFor(aboard));
        Assert.Equal("Core for Oxen Free (Imperial Cutter)", core.CaptionFor(picked));
    }

    [Fact]
    public void WithNoShipKnownTheCoreRowKeepsItsOldLabel()
    {
        var store = new ShipCoreStore(
            Path.Combine(MemoryInstall.FakeRoot, $"d47-ship-cores-{Guid.NewGuid():N}.json"), new MemoryFileSystem(),
            NullLogger<ShipCoreStore>.Instance);

        var ships = new ShipCoreService(store, () => null);
        var core = PersonaCapability.Create(new PersonaHost(), TestSurface.For(new MemoryInstall()).Settings, ships)
            .Settings.First(row => row.Key == PersonaCapability.ShipCoreKey);

        Assert.Equal("Core for that ship", core.CaptionFor(D47Settings.Defaults));
        Assert.Equal("Core for that ship", core.Label);
    }

    private static (SettingRow Ship, SettingRow Core, D47Settings Settings) Rows(
        int picked = 0,
        params (int Ship, string Core)[] bound) =>
        Rows(out _, picked, bound);

    private static (SettingRow Ship, SettingRow Core, D47Settings Settings) Rows(
        out ShipCoreService ships,
        int picked = 0,
        params (int Ship, string Core)[] bound)
    {
        var state = new GameStateStore();

        Apply(state, """{"timestamp":"2026-08-19T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(state, """{"timestamp":"2026-08-19T09:00:00Z","event":"Loadout","Ship":"python","ShipID":7,"ShipName":"Bad Idea","Modules":[]}""");
        Apply(
            state,
            """
            { "timestamp":"2026-08-21T11:00:00Z", "event":"StoredShips", "StarSystem":"Deciat",
              "StationName":"Garay Terminal",
              "ShipsHere":[ {"ShipID":53,"ShipType":"cutter","ShipType_Localised":"Imperial Cutter",
                             "Name":"Oxen Free","Value":700000000} ],
              "ShipsRemote":[] }
            """);

        var store = new ShipCoreStore(
            Path.Combine(MemoryInstall.FakeRoot, $"d47-ship-cores-{Guid.NewGuid():N}.json"), new MemoryFileSystem(),
            NullLogger<ShipCoreStore>.Instance);

        foreach (var (id, core) in bound)
        {
            store.Bind(new ShipCoreBinding("F1", id, core));
        }

        ships = new ShipCoreService(store, () => state.Active);

        var rows = PersonaCapability.Create(new PersonaHost(), TestSurface.For(new MemoryInstall()).Settings, ships).Settings;
        var settings = D47Settings.Defaults with
        {
            Persona = D47Settings.Defaults.Persona with { ShipCoreShip = picked },
        };

        return (
            rows.First(row => row.Key == PersonaCapability.ShipCoreShipKey),
            rows.First(row => row.Key == PersonaCapability.ShipCoreKey),
            settings);
    }

    private static void Apply(GameStateStore state, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        state.Apply(parsed!);
    }
}
