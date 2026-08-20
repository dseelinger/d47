using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// A build and the ship it was written for are the same ship in either spelling (reported
/// 2026-08-20).
/// <para>
/// <b>Frontier writes the hull two ways and d47 stored whichever it was handed.</b>
/// <c>Loadout</c> carries <c>panthermkii</c>; <c>StoredShips</c> carries
/// <c>ShipType_Localised</c> — "Panther Clipper Mk II" — where Frontier supplies one, which is
/// what <see cref="StoredShip.Type"/> reads and therefore what a build started from the fleet
/// was created with. Four of seven builds in the reported file held a display name.
/// </para>
/// <para>
/// Compared as text those are two different ships, so every slot of that plan reported
/// <see cref="ChecklistState.Stale"/> against the very ship it was written for — <i>"That ship id
/// now reports a panthermkii, and this plan was written for a Panther Clipper Mk II"</i> — and
/// <c>EliteSpecifications.Slots</c> found no layout under the display name, so the page could not
/// show an empty slot either.
/// </para>
/// </summary>
public class AHullIsAHullHoweverItIsSpeltTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState InThePanther()
    {
        var store = new GameStateStore();

        store.Apply(Event(
            """{"timestamp":"2026-08-20T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        store.Apply(Event(
            """
            {"timestamp":"2026-08-20T01:00:00Z","event":"Loadout","Ship":"panthermkii","ShipID":41,
             "Modules":[{"Slot":"LifeSupport","Item":"int_lifesupport_size5_class2","On":true,"Health":1.0}]}
            """));

        return store.Active!;
    }

    private static ChecklistItem PlannedFor(string hull) => new()
    {
        Key = "bp/lifesupport",
        Scope = ChecklistScope.Ship(41),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = "Grade 5 Lightweight on Life Support",
        Hull = hull,
        Intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "LifeSupport")
        {
            Detail = "Lightweight",
            Grade = 5,
        },
    };

    [Fact]
    public void ThePlanIsNotStaleWhenItNamesTheSameHullInFrontiersOtherSpelling()
    {
        // The reported sentence, and it was about the ship the Commander was sitting in.
        var verdict = ChecklistEvaluator.Evaluate(PlannedFor("Panther Clipper Mk II"), InThePanther());

        Assert.NotNull(verdict);
        Assert.NotEqual(ChecklistState.Stale, verdict.Value.State);
    }

    [Fact]
    public void ThePlanIsNotStaleWhenItNamesTheSymbolEither()
    {
        var verdict = ChecklistEvaluator.Evaluate(PlannedFor("panthermkii"), InThePanther());

        Assert.NotNull(verdict);
        Assert.NotEqual(ChecklistState.Stale, verdict.Value.State);
    }

    [Fact]
    public void APlanForADifferentShipIsStillStale()
    {
        // The check this fix must not disarm. Swapping a ShipID onto another hull is exactly what
        // Stale exists to catch, and it is caught in either spelling.
        Assert.Equal(
            ChecklistState.Stale,
            ChecklistEvaluator.Evaluate(PlannedFor("Imperial Cutter"), InThePanther())!.Value.State);

        Assert.Equal(
            ChecklistState.Stale,
            ChecklistEvaluator.Evaluate(PlannedFor("cutter"), InThePanther())!.Value.State);
    }

    [Fact]
    public void AStoredBuildIsReadBackAsTheJournalsOwnSpelling()
    {
        // Files already on disk hold the display name, and so will anything a Commander types by
        // hand. Normalised on the way in rather than refused, so no migration is needed and the
        // layout lookup — which is keyed on the symbol — finds the hull.
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"d47-builds-{Guid.NewGuid():N}.json");

        try
        {
            System.IO.File.WriteAllText(
                path,
                """
                {"ships":[{"id":"ship-1","hull":"Panther Clipper Mk II","shipId":41,"name":"Ox"}]}
                """);

            var store = new ShipBuildStore(path, NullLogger<ShipBuildStore>.Instance);

            Assert.True(store.Poll());
            Assert.Empty(store.Problems);
            Assert.Equal("panthermkii", Assert.Single(store.Builds).Hull);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void AHullNothingKnowsStandsAsItWasWritten()
    {
        // The honest remainder. A spelling the table does not carry is not silently replaced, and
        // is not refused either — Frontier ships hulls before any table is regenerated.
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"d47-builds-{Guid.NewGuid():N}.json");

        try
        {
            System.IO.File.WriteAllText(
                path,
                """
                {"ships":[{"id":"ship-1","hull":"Thargoid Interceptor","shipId":900}]}
                """);

            var store = new ShipBuildStore(path, NullLogger<ShipBuildStore>.Instance);

            Assert.True(store.Poll());
            Assert.Equal("Thargoid Interceptor", Assert.Single(store.Builds).Hull);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
