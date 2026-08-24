using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Loadout;

/// <summary>
/// The gap between every plan and what the Commander is carrying (list.md Phase 27, "Gap
/// analysis").
/// <para>
/// <b>Not a wishlist</b>, and <b>the ledgers are never totalled together</b>. Those are the two
/// claims this file exists to hold: the first is why a shortfall reads back to what asked for it,
/// and the second is why a single headline number over the top of the ledgers would be a lie.
/// </para>
/// </summary>
public class PlanGapTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    /// <summary>
    /// A Commander flying a Python, rank 5 with Farseer, holding some iron and on foot in a grade
    /// 3 Maverick with nothing in the locker.
    /// </summary>
    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":5}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,"Priority":0,"Health":1.0}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[{"Name":"iron","Count":300},{"Name":"sulphur","Count":2}],"Manufactured":[],"Encoded":[]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[]}""",
                 })
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static ShipBuild Ship(int? shipId = 12) =>
        new("F1", "ship-1", "python", shipId, "Bad Idea",
            [new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer")]);

    private static OnFootBuild Suit(long? itemId = 7) =>
        new("kit-1", "Maverick Suit", OnFootKind.Suit, itemId,
            [new KitPlan(OnFootBuild.GradeSlot, 5)]);

    /// <summary>
    /// Ship materials and ship-locker micro-resources are separate ledgers with separate caps and
    /// no exchange between them, so they are reported apart and never summed.
    /// </summary>
    [Fact]
    public void TheLedgersAreKeptApart()
    {
        var report = PlanGap.Of([Ship()], [Suit()], State());

        Assert.Equal(2, report.Plans);
        Assert.Contains(report.Ledgers, ledger => ledger.Ledger == MaterialLedger.Material);
        Assert.Contains(report.Ledgers, ledger => ledger.Ledger == MaterialLedger.ShipLocker);

        // The one figure that spans everything is a count of units to find, and it is the sum of
        // the per-ledger counts rather than a total of anything held.
        Assert.Equal(report.Ledgers.Sum(ledger => ledger.UnitsToFind), report.UnitsToFind);
    }

    /// <summary>
    /// A shortfall reads back to what wants it — the ships and slots that asked. That is what
    /// makes the roll-up navigable instead of merely a total.
    /// </summary>
    [Fact]
    public void AShortfallNamesWhatWantsIt()
    {
        var report = PlanGap.Of([Ship()], [Suit()], State());

        var lines = report.Ledgers.SelectMany(ledger => ledger.Lines).ToList();

        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.NotEmpty(line.Wanted));

        Assert.Contains(
            lines.SelectMany(line => line.Wanted),
            demand => demand.What.Contains("MainEngines", StringComparison.Ordinal));

        Assert.Contains(
            lines.SelectMany(line => line.Wanted),
            demand => demand.What.Contains("Maverick", StringComparison.Ordinal));
    }

    /// <summary>
    /// Two slots wanting the same material are one line with both askers on it, and the need is
    /// the sum — which is the arithmetic nobody can do in their head.
    /// </summary>
    [Fact]
    public void TwoSlotsWantingOneMaterialAreOneLineWithBothAskers()
    {
        var build = Ship() with
        {
            Slots =
            [
                new SlotPlan("MainEngines", "Dirty Drive Tuning", 3, "Felicity Farseer"),
                new SlotPlan("PowerPlant", "Dirty Drive Tuning", 3, "Felicity Farseer"),
            ],
        };

        var report = PlanGap.Of([build], [], State());
        var line = report.Ledgers.SelectMany(ledger => ledger.Lines).First();

        Assert.Equal(2, line.Wanted.Count);
        Assert.Equal(line.Wanted.Sum(demand => demand.Units), line.Needed);
    }

    /// <summary>
    /// Whether hulls the Commander does not own count is a filter rather than a decision taken on
    /// their behalf: counting them is honest about the whole ambition, and excluding them answers
    /// what can be finished now.
    /// </summary>
    [Fact]
    public void CountingWhatIsNotOwnedIsAFilter()
    {
        var intended = Ship(shipId: null);

        var counted = PlanGap.Of([intended], [], State());
        var excluded = PlanGap.Of([intended], [], State(), includeIntended: false);

        Assert.Equal(1, counted.Plans);
        Assert.NotEmpty(counted.Ledgers);

        Assert.Equal(0, excluded.Plans);
        Assert.True(excluded.IsEmpty);

        // Either way it says how many were left out, so the filter is reported rather than
        // silently applied.
        Assert.Equal(1, counted.Intended);
        Assert.Equal(1, excluded.Intended);
        Assert.False(excluded.IncludesIntended);
    }

    /// <summary>
    /// Nothing carried, nothing planned: an empty answer means "nothing is planned" rather than
    /// "you have everything", and the page needs to be able to tell those apart.
    /// </summary>
    [Fact]
    public void NoPlansIsADifferentAnswerFromNoShortfall()
    {
        var nothing = PlanGap.Of([], [], State());

        Assert.Equal(0, nothing.Plans);
        Assert.True(nothing.IsEmpty);
    }

    /// <summary>
    /// A material trader can cover a shortfall, and the offer is a second line beside the honest
    /// raw number rather than a reason to report a smaller one.
    /// </summary>
    [Fact]
    public void TradeIsOfferedBesideTheShortfallAndNeverInsteadOfIt()
    {
        var build = Ship() with
        {
            Slots = [new SlotPlan("MainEngines", "Dirty Drive Tuning", 3, "Felicity Farseer")],
        };

        var report = PlanGap.Of([build], [], State());

        var traded = report.Ledgers
            .SelectMany(ledger => ledger.Lines)
            .Where(line => line.Trade is not null)
            .ToList();

        Assert.NotEmpty(traded);

        foreach (var line in traded)
        {
            // The shortfall is untouched by the offer existing.
            Assert.Equal(line.Needed - line.Held, line.Short);

            // And the offer covers it out of something the Commander actually holds spare - here
            // the 300 iron, which nothing in the plan wants.
            Assert.True(line.Trade!.Get >= line.Short);
            Assert.NotEqual(line.Material.Symbol, line.Trade.From.Symbol);

            // And it says who would make the trade. "or trade 24 Iron for 8" states a rate with no
            // owner, and a turn carrying one ownerless offer beside one engineer's name produced
            // an answer offering that engineer's trade (bugs.md, reported 2026-08-20).
            Assert.StartsWith("A material trader would take ", line.Trade.Describe(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A rank gate is not a shortfall: it is stated rather than costed, because listing materials
    /// under a gate nobody can pass is listing work nobody can start.
    /// </summary>
    [Fact]
    public void ARankGateIsStatedRatherThanCosted()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":2}]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","Modules":[]}""",
                 })
        {
            store.Apply(Event(line));
        }

        var report = PlanGap.Of([Ship()], [], store.Active);

        Assert.NotEmpty(report.Gates);
        Assert.True(report.IsEmpty);
    }

    /// <summary>
    /// A wildcard grade is a real intent and an uncostable one. Kept and marked, never refused:
    /// a plan line presses nothing, so the honest move is to carry it and say what is unknown.
    /// </summary>
    [Fact]
    public void AnUncostableLineIsKeptAndMarked()
    {
        var build = Ship() with
        {
            Slots = [new SlotPlan("MainEngines", "Dirty Drive Tuning", Grade: 0)],
        };

        var report = PlanGap.Of([build], [], State());

        Assert.NotEmpty(report.Uncovered);
    }
}
