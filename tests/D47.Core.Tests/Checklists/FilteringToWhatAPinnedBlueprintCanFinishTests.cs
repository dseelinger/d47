using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>Reading the checklist as "what can I finish anywhere", through a pinned blueprint (#113).</summary>
public class FilteringToWhatAPinnedBlueprintCanFinishTests
{
    private const int LeiCheung = 300120;

    /// <summary>Not offered where nothing is pinned, which is the overwhelmingly common case.</summary>
    [Fact]
    public void TheRowIsAbsentWhereNothingIsPinned()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        Assert.DoesNotContain(
            checklists.FilterAxes(),
            filter => filter.Key == ChecklistService.PinnedKey);
    }

    /// <summary>
    /// Offered the moment anything is pinned, unlike "here", which needs the engineer's system —
    /// (#113).
    /// </summary>
    [Fact]
    public void TheRowAppearsAsSoonAsAnythingIsPinned()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.Pin(LeiCheung, pinned: true);

        Assert.Contains(
            checklists.FilterAxes(),
            filter => filter.Key == ChecklistService.PinnedKey);
    }

    /// <summary>Its own heading, not "Where you are" — this one is about what is reachable anywhere.</summary>
    [Fact]
    public void ItSitsUnderItsOwnHeading()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        checklists.Pin(LeiCheung, pinned: true);

        var row = Assert.Single(checklists.FilterAxes(), filter => filter.Key == ChecklistService.PinnedKey);

        Assert.NotEqual("Where you are", row.Heading);
    }

    /// <summary>Rank gates a pinned line exactly as it gates one done at the workshop.</summary>
    [Fact]
    public void APinnedEngineerIsAskedRegardlessOfWhereTheCommanderStands()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths, Flying("Shinrarta Dezhra", rank: 5));

        checklists.AdoptPlan(
            ChecklistScope.Ship(51),
            ChecklistSource.EngineeringPlan,
            [Booster("TinyHardpoint5", grade: 3)],
            ["TinyHardpoint5"]);

        var item = Assert.Single(checklists.Document.Items);

        // Nowhere near Lei Cheung, and "here" says so — the row a pin is not.
        Assert.False(checklists.OfferedHere(item));
        Assert.False(checklists.OfferedPinned(item));

        checklists.Pin(LeiCheung, pinned: true);

        Assert.True(checklists.OfferedPinned(item));
    }

    /// <summary>A line the pin only covers part of the way is not kept — this filter is about finishing.</summary>
    [Fact]
    public void ALineOutOfRankWithThePinnedEngineerIsNotKept()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths, Flying("Shinrarta Dezhra", rank: 1));

        checklists.AdoptPlan(
            ChecklistScope.Ship(51),
            ChecklistSource.EngineeringPlan,
            [Booster("TinyHardpoint5", grade: 3)],
            ["TinyHardpoint5"]);

        var item = Assert.Single(checklists.Document.Items);

        checklists.Pin(LeiCheung, pinned: true);

        Assert.False(checklists.OfferedPinned(item));
    }

    /// <summary>A pin toggles both ways, idempotently.</summary>
    [Fact]
    public void APinIsSetAndCleared()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        Assert.False(checklists.IsPinned(LeiCheung));

        checklists.Pin(LeiCheung, pinned: true);
        Assert.True(checklists.IsPinned(LeiCheung));

        checklists.Pin(LeiCheung, pinned: true);
        Assert.True(checklists.IsPinned(LeiCheung));

        checklists.Pin(LeiCheung, pinned: false);
        Assert.False(checklists.IsPinned(LeiCheung));
    }

    /// <summary>Pinning raises the event a surface under the row redraws from.</summary>
    [Fact]
    public void PinningRaisesPinnedChanged()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        var raised = 0;
        checklists.PinnedChanged += () => raised++;

        checklists.Pin(LeiCheung, pinned: true);

        Assert.Equal(1, raised);
    }

    /// <summary>A pin outlives the session, the same as the chosen filter does.</summary>
    [Fact]
    public void APinRoundTripsThroughRememberAndRestore()
    {
        ChecklistView? remembered = null;

        using var install = new TempInstall();
        var checklists = new ChecklistService(
            new ChecklistStore(
                System.IO.Path.Combine(install.Paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                System.IO.Path.Combine(install.Paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null,
            view => remembered = view);

        checklists.Pin(LeiCheung, pinned: true);

        Assert.NotNull(remembered);
        Assert.Contains(LeiCheung, remembered!.PinnedEngineers!);

        var restored = TestSurface.EmptyChecklists(install.Paths.Data);
        restored.Restore(remembered);

        Assert.True(restored.IsPinned(LeiCheung));
    }

    /// <summary>
    /// The key is a constant rather than a spelling, because the panel matches filter keys against enum
    /// names and this one is not an enum.
    /// </summary>
    [Fact]
    public void TheKeyIsOneSpelling() => Assert.Equal("pinned", ChecklistService.PinnedKey);

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
