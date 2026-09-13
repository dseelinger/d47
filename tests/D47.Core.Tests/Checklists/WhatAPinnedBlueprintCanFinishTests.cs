using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>What a blueprint pinned with an engineer could finish, wherever the Commander is standing.</summary>
public class WhatAPinnedBlueprintCanFinishTests
{
    private const int LeiCheung = 300120;

    /// <summary>The whole point of a pin: nowhere near the engineer, and still theirs to roll.</summary>
    [Fact]
    public void APinnedEngineerIsAskedRegardlessOfWhereTheCommanderStands()
    {
        var state = Flying("Shinrarta Dezhra", rank: 5).Active!;
        var items = new[] { Booster("TinyHardpoint5", grade: 3) };

        // Nobody is based here, so the location-bound question answers with nothing.
        Assert.Empty(EngineersHere.For(items, state));

        var pinned = Assert.Single(EngineersPinned.For(items, state, [LeiCheung]));

        Assert.Equal("Lei Cheung", pinned.Engineer.Name);
        Assert.Single(pinned.Ready);
    }

    /// <summary>An id nothing is pinned with is asked nothing.</summary>
    [Fact]
    public void NoPinIsNothingToSay()
    {
        var state = Flying("Shinrarta Dezhra", rank: 5).Active!;
        var items = new[] { Booster("TinyHardpoint5", grade: 3) };

        Assert.Empty(EngineersPinned.For(items, state, []));
    }

    /// <summary>Work below the Commander's grade with the pinned engineer is still not finished today.</summary>
    [Fact]
    public void WorkBeyondTheCommandersGradeIsStillOutOfRank()
    {
        var state = Flying("Shinrarta Dezhra", rank: 1).Active!;
        var items = new[] { Booster("TinyHardpoint5", grade: 3) };

        var pinned = Assert.Single(EngineersPinned.For(items, state, [LeiCheung]));

        Assert.Empty(pinned.Ready);
        Assert.Single(pinned.OutOfRank);
    }

    /// <summary>One line of an engineering plan: a blueprint wanted on a slot, at a grade.</summary>
    private static ChecklistItem Booster(string slot, int grade, int shipId = 51) => new()
    {
        Key = $"blueprint:{slot}",
        Scope = ChecklistScope.Ship(shipId),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = $"Grade {grade} Heavy Duty on {slot}",
        Intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, slot)
        {
            Detail = "Heavy Duty",
            Grade = grade,
        },
    };

    /// <summary>In a system, in an Anaconda with a shield booster fitted, and known to Lei Cheung.</summary>
    private static GameStateStore Flying(string system, int rank)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     $$"""{"timestamp":"2026-08-20T09:00:01Z","event":"Location","StarSystem":"{{system}}","Docked":true,"StationName":"Trader's Rest"}""",
                     $$"""{"timestamp":"2026-08-20T09:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":{{LeiCheung}},"Progress":"Unlocked","Rank":{{rank}}}]}""",
                     """{"timestamp":"2026-08-20T09:00:03Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"TinyHardpoint5","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }
}
