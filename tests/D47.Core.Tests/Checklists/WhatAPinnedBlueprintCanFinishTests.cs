using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Checklists.ChecklistFixtures;

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

}
