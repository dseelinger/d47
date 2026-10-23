using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>With no login in the newest journal, the last ship seen is the one the Commander is in.</summary>
public class ALoggedOutCommanderStillHasAShipTests
{
    private const string Commander = """{"timestamp":"2026-10-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private const string Stored = """
        {"timestamp":"2026-10-20T09:00:01Z","event":"StoredShips","StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra",
         "ShipsHere":[{"ShipID":9,"ShipType":"python","Name":"Rack"}],"ShipsRemote":[]}
        """;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static ShipLoadouts Remembered(int shipId) =>
        ShipLoadouts.Empty.Remember(
            ShipLoadout.Unknown.Apply(Parse(
                $$"""{"timestamp":"2026-10-19T10:00:00Z","event":"Loadout","Ship":"anaconda","ShipID":{{shipId}},"ShipName":"Last","Modules":[]}""")),
            DateTimeOffset.Parse("2026-10-19T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static GameStateStore LoggedOut(ShipLoadouts? remembered, params string[] more)
    {
        var game = new GameStateStore { RestoreLoadouts = _ => remembered };

        game.Apply(Parse(Commander));
        game.Apply(Parse(Stored));

        foreach (var json in more)
        {
            game.Apply(Parse(json));
        }

        return game;
    }

    private static ShipPlanService Plans(GameStateStore game)
    {
        var files = new MemoryFileSystem();
        var install = new MemoryInstall();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(install.Root, "checklist.json"), files, NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"), files, NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        return new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);
    }

    [Fact]
    public void TheLastSeenShipIsTheActiveEntry()
    {
        var game = LoggedOut(Remembered(5));

        var active = Assert.Single(Plans(game).Fleet(), entry => entry.IsActive);

        Assert.Equal(5, active.Stored!.ShipId);
        Assert.True(active.LastFlown);
        Assert.Equal(2, Plans(game).Fleet().Count);
    }

    [Fact]
    public void ASwappedShipNowInStorageIsNotTakenForTheOneFlown()
    {
        var game = LoggedOut(Remembered(9));

        Assert.DoesNotContain(Plans(game).Fleet(), entry => entry.IsActive);
        Assert.Null(game.Active!.LastFlownShip);
    }

    [Fact]
    public void NoRememberedLoadoutMeansNoActiveShip()
    {
        var game = LoggedOut(remembered: null);

        Assert.DoesNotContain(Plans(game).Fleet(), entry => entry.IsActive);
    }

    [Fact]
    public void ALoginReplacesTheRememberedShip()
    {
        var game = LoggedOut(
            Remembered(5),
            """{"timestamp":"2026-10-23T09:00:00Z","event":"Loadout","Ship":"python","ShipID":9,"ShipName":"Rack","Modules":[]}""");

        var active = Assert.Single(Plans(game).Fleet(), entry => entry.IsActive);

        Assert.False(active.LastFlown);
        Assert.Equal(9, active.Stored!.ShipId);
        Assert.Null(game.Active!.LastFlownShip);
    }

    [Fact]
    public async Task TheSpokenFleetNamesTheShipAsLastFlown()
    {
        var game = LoggedOut(Remembered(5));
        var registry = CapabilityRegistry.Build([JournalCapability.Create(game, () => HistoryState.Done)]);

        var result = await registry.InvokeAsync(
            "get_fleet", ToolArguments.FromJson("""{"ships":true}"""), TestContext.Current.CancellationToken);

        Assert.Contains("Last flown", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Currently flying", result.Content, StringComparison.Ordinal);
    }
}
