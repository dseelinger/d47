using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>The book raises <see cref="AdventureBook.StirringChanged"/> when a count or a step moves, so every panel showing the objective redraws.</summary>
public sealed class AMovedObjectiveRedrawsTheStoryPanelsTests
{
    private const string Key = "the-lantern-route";

    private static readonly string Folder = Path.Combine(MemoryInstall.FakeRoot, "moved-objective");
    private static readonly string AdventuresFile = Path.Combine(Folder, "adventures.json");
    private static readonly string JournalFile = Path.Combine(Folder, "Journal.2026-08-22T194000.01.log");

    private static readonly AdventureTrigger FourBounties = new() { Kind = TriggerKind.Bounty, Count = 4 };
    private static readonly AdventureTrigger Arrive = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    private readonly MemoryFileSystem _files = new();
    private readonly AdventureBook _book;
    private int _raised;

    public AMovedObjectiveRedrawsTheStoryPanelsTests()
    {
        _book = new AdventureBook(new AdventureStore(AdventuresFile, _files, NullLogger<AdventureStore>.Instance), NullLogger<AdventureBook>.Instance);
    }

    private static DateTimeOffset At(int minutes) => Accepted.AddMinutes(minutes);

    private static JournalEvent BountyAt(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Bounty", "Rewards":[{"Faction":"Federation","Reward":500}], "Target":"sidewinder", "TotalReward":500, "VictimFaction":"Pirates" }""");

    /// <summary>The Lantern Route with its first beat made of <paramref name="steps"/>.</summary>
    private static Adventure FirstBeat(DateTimeOffset acceptedAt, params AdventureTrigger[] steps)
    {
        var route = LanternRoute(acceptedAt);

        return route with { Beats = [route.Beats[0] with { Steps = [.. steps.Select(trigger => new BeatStep { Trigger = trigger })] }, .. route.Beats.Skip(1)] };
    }

    private void Begin(Adventure adventure)
    {
        Assert.Null(_book.Write("F1", adventure));
        _book.CatchUp([]);
        _book.StirringChanged += () => _raised++;
    }

    private string? Next() => _book.Standing("F1", Key)!.NextTrigger();

    [Fact]
    public void ACountedEventRaisesTheChangeAndMovesTheCount()
    {
        Begin(FirstBeat(Accepted, FourBounties));
        Assert.Contains("0 of 4", Next(), StringComparison.Ordinal);

        _book.Observe(BountyAt(At(1)), "F1");

        Assert.Equal(1, _raised);
        Assert.Contains("1 of 4", Next(), StringComparison.Ordinal);
        Assert.False(_book.IsStirring("F1", Key));
    }

    [Fact]
    public void AJumpThatMovesNoObjectiveRaisesNothing()
    {
        Begin(FirstBeat(Accepted, FourBounties));
        var before = Next();

        _book.Observe(Jump(QuietField, At(1)), "F1");

        Assert.Equal(QuietField, _book.Standing("F1", Key)!.SystemAddress);
        Assert.Equal(0, _raised);
        Assert.Equal(before, Next());
    }

    [Fact]
    public void AStepHandOffRaisesTheChange()
    {
        Begin(FirstBeat(Accepted, Arrive, FourBounties));

        _book.Observe(Jump(Lantern, At(1), "Ossen's Lantern"), "F1");

        Assert.Equal(1, _raised);
        Assert.Contains("0 of 4", Next(), StringComparison.Ordinal);
    }

    [Fact]
    public void AWalkThatMovesTheCountRaisesTheChange()
    {
        _files.WriteText(JournalFile, string.Join('\n', new[] { Commander("F1", At(-1)), BountyAt(At(1)) }.Select(e => e.Raw.GetRawText())) + "\n");
        Begin(FirstBeat(Accepted.AddDays(-1), FourBounties));

        _book.Store.Save("F1", FirstBeat(Accepted, FourBounties));
        _book.Reconcile();
        Assert.True(_book.NeedsCatchUp);

        _book.Adopt(_book.StartWalk(Folder, null)!.Run());

        Assert.Equal(1, _raised);
        Assert.Contains("1 of 4", Next(), StringComparison.Ordinal);
    }
}
