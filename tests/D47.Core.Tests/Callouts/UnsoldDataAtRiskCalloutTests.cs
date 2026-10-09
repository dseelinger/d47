using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The warning when unsold exploration data goes into danger (#638).</summary>
public class UnsoldDataAtRiskCalloutTests
{
    private const string Fid = "F1234";

    private const long System = 3274702866819;

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Drop(string type) =>
        Parse($$"""{"timestamp":"2026-09-05T12:00:00Z","event":"SupercruiseDestinationDrop","Type":"{{type}}","Threat":3,"MarketID":0}""");

    private static JournalEvent Hull(double health, bool playerPilot = true, bool fighter = false) =>
        Parse($$"""{"timestamp":"2026-09-05T12:00:00Z","event":"HullDamage","Health":{{health}},"PlayerPilot":{{playerPilot.ToString().ToLowerInvariant()}},"Fighter":{{fighter.ToString().ToLowerInvariant()}}}""");

    /// <summary>Earth-like worlds, scanned and mapped efficiently; each is worth millions.</summary>
    private static CartographyLedger Maps(int count, bool folded = true)
    {
        var ledger = new CartographyLedger(null, new MemoryFileSystem(), NullLogger.Instance);
        var events = new List<JournalEvent>();

        for (var body = 1; body <= count; body++)
        {
            events.Add(Parse($$"""{"timestamp":"2026-09-05T11:00:00Z","event":"Scan","ScanType":"Detailed","BodyName":"Body {{body}}","BodyID":{{body}},"StarSystem":"Praea Euq","SystemAddress":{{System}},"PlanetClass":"Earthlike body","TerraformState":"","MassEM":1.0,"WasDiscovered":false,"WasMapped":false}"""));
            events.Add(Parse($$"""{"timestamp":"2026-09-05T11:10:00Z","event":"SAAScanComplete","BodyName":"Body {{body}}","BodyID":{{body}},"SystemAddress":{{System}},"ProbesUsed":4,"EfficiencyTarget":6}"""));
        }

        ledger.Apply(events, Fid);

        if (folded)
        {
            ledger.FoldHistory([]);
        }

        return ledger;
    }

    private static ExobiologyLedger Biology(bool folded = true)
    {
        var ledger = new ExobiologyLedger(null, new MemoryFileSystem(), NullLogger.Instance);

        if (folded)
        {
            ledger.FoldHistory([]);
        }

        return ledger;
    }

    private static List<Announcement> Examine(
        UnsoldDataAtRiskCallout callout,
        JournalEvent journalEvent,
        bool priming = false)
    {
        var state = new CommanderGameState(new CommanderIdentity(Fid, "Doug"));
        var context = new CalloutContext(Noon, priming, state, new GameStatus(), new NavRoute(), [journalEvent]);
        return [.. callout.Examine(context)];
    }

    [Fact]
    public void HeldMapsWorthMoreThanFiveMillionWarnOnceOnAWarzoneDrop()
    {
        var maps = Maps(count: 12);

        Assert.True(maps.Unsold(Fid).Total >= UnsoldDataAtRiskCallout.Threshold);

        var said = Assert.Single(Examine(new UnsoldDataAtRiskCallout(maps, Biology()), Drop("$Warzone_PointRace_Low;")));

        Assert.Equal(UnsoldDataAtRiskCallout.WarzoneKey, said.Key);
        Assert.StartsWith("You are carrying ", said.Text);
        Assert.EndsWith(" credits of unsold data into a fight.", said.Text);
        Assert.Equal(TimeSpan.FromMinutes(30), said.Cooldown);
    }

    [Fact]
    public void LessThanFiveMillionSaysNothing()
    {
        var maps = Maps(count: 1);

        Assert.True(maps.Unsold(Fid).Total is > 0 and < UnsoldDataAtRiskCallout.Threshold);

        Assert.Empty(Examine(new UnsoldDataAtRiskCallout(maps, Biology()), Drop("$Warzone_PointRace_Low;")));
    }

    [Fact]
    public void AHazardousExtractionSiteWarnsAndAHighOneDoesNot()
    {
        var callout = new UnsoldDataAtRiskCallout(Maps(count: 12), Biology());

        var said = Assert.Single(Examine(callout, Drop("$MULTIPLAYER_SCENARIO79_TITLE;")));

        Assert.Equal(UnsoldDataAtRiskCallout.ExtractionSiteKey, said.Key);
        Assert.Empty(Examine(callout, Drop("$MULTIPLAYER_SCENARIO78_TITLE;")));
    }

    [Fact]
    public void AFailingHullWarnsOnlyForTheCommandersOwnShipBelowSixtyPercent()
    {
        var callout = new UnsoldDataAtRiskCallout(Maps(count: 12), Biology());

        var said = Assert.Single(Examine(callout, Hull(0.55)));

        Assert.Equal(UnsoldDataAtRiskCallout.HullKey, said.Key);
        Assert.StartsWith("Hull is failing with ", said.Text);
        Assert.EndsWith(" credits of unsold data aboard.", said.Text);
        Assert.Empty(Examine(callout, Hull(0.65)));
        Assert.Empty(Examine(callout, Hull(0.3, fighter: true)));
        Assert.Empty(Examine(callout, Hull(0.3, playerPilot: false)));
    }

    [Fact]
    public void NothingIsSaidWhilePrimingOrBeforeHistoryIsFolded()
    {
        Assert.Empty(Examine(new UnsoldDataAtRiskCallout(Maps(count: 12), Biology()), Drop("$Warzone_PointRace_Low;"), priming: true));
        Assert.Empty(Examine(new UnsoldDataAtRiskCallout(Maps(count: 12, folded: false), Biology()), Drop("$Warzone_PointRace_Low;")));
        Assert.Empty(Examine(new UnsoldDataAtRiskCallout(Maps(count: 12), Biology(folded: false)), Drop("$Warzone_PointRace_Low;")));
    }
}
