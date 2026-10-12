using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

public class EachCommanderKeepsTheirOwnPinnedEngineersTests
{
    private const int LeiCheung = 300120;
    private const int FeliciaWinters = 300270;

    private static readonly CommanderIdentity Kestrel = new("F1", "Kestrel Vane");
    private static readonly CommanderIdentity Doug = new("F2", "Doug");

    private static ChecklistService Checklists(GameStateStore states, Action<ChecklistView>? remember = null)
    {
        var install = new MemoryInstall();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(install.Paths.Data, "checklist.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => states.Active,
            remember);

        checklists.Follow(states);

        return checklists;
    }

    [Fact]
    public void TwoCommandersKeepSeparatePins()
    {
        var states = new GameStateStore();
        var checklists = Checklists(states);

        states.Pick(Kestrel);
        checklists.Pin(LeiCheung, pinned: true);

        states.Pick(Doug);
        Assert.False(checklists.IsPinned(LeiCheung));
        checklists.Pin(FeliciaWinters, pinned: true);

        states.Pick(Kestrel);
        Assert.True(checklists.IsPinned(LeiCheung));
        Assert.False(checklists.IsPinned(FeliciaWinters));
    }

    [Fact]
    public void PickingACommanderRaisesPinnedChanged()
    {
        var states = new GameStateStore();
        var checklists = Checklists(states);
        var raised = 0;

        checklists.PinnedChanged += () => raised++;

        states.Pick(Kestrel);
        states.Pick(Doug);

        Assert.Equal(2, raised);
    }

    [Fact]
    public void PinsSavedWithoutACommanderGoToTheFirstCommanderShown()
    {
        var states = new GameStateStore();
        var checklists = Checklists(states);

        checklists.Restore(new ChecklistView("everything", false, PinnedEngineers: [LeiCheung]));

        states.Pick(Kestrel);
        Assert.True(checklists.IsPinned(LeiCheung));

        states.Pick(Doug);
        Assert.False(checklists.IsPinned(LeiCheung));
    }

    [Fact]
    public void PinsRoundTripPerCommanderThroughRememberAndRestore()
    {
        ChecklistView? remembered = null;
        var states = new GameStateStore();
        var checklists = Checklists(states, view => remembered = view);

        states.Pick(Kestrel);
        checklists.Pin(LeiCheung, pinned: true);
        states.Pick(Doug);
        checklists.Pin(FeliciaWinters, pinned: true);

        var later = new GameStateStore();
        var restored = Checklists(later);
        restored.Restore(remembered);

        later.Pick(Doug);
        Assert.True(restored.IsPinned(FeliciaWinters));
        Assert.False(restored.IsPinned(LeiCheung));

        later.Pick(Kestrel);
        Assert.True(restored.IsPinned(LeiCheung));
    }
}
