using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>Drives the Lantern Route to its midpoint (third beat) and its all-is-lost beat (fourth).</summary>
public abstract class BackstoryNudgeTestBase
{
    private static readonly string AdventuresFile = Path.Combine(MemoryInstall.FakeRoot, "adventures.json");

    protected AdventureCallout Wired(bool backstory = true, bool switchedOn = true)
    {
        var store = new AdventureStore(AdventuresFile, new MemoryFileSystem(), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write("F1", LanternRoute(Accepted));
        book.CatchUp([]);

        return new AdventureCallout(book)
        {
            Settle = TimeSpan.FromSeconds(20),
            HasBackstory = () => backstory,
            NudgeBackstory = () => switchedOn,
        };
    }

    private static CalloutContext At(DateTimeOffset now, IReadOnlyList<JournalEvent> events, GameStatus? status = null) =>
        new(now, false, new CommanderGameState(new CommanderIdentity("F1", "Tester")), status ?? GameStatus.Unknown, NavRoute.None, events);

    /// <summary>Fires each beat in turn, letting it settle, and returns everything said for the last one.</summary>
    protected static List<Announcement> Through(AdventureCallout callout, int beats, GameStatus? dueStatus = null)
    {
        var route = WholeRoute(Accepted);
        var said = new List<Announcement>();

        for (var index = 0; index < beats; index++)
        {
            var at = route[index].Timestamp;
            said = [.. callout.Examine(At(at, [route[index]]))];
            said.AddRange(callout.Examine(At(at.AddSeconds(30), [], index == beats - 1 ? dueStatus : null)));
        }

        return said;
    }

    protected static bool IsNudge(Announcement announcement) =>
        announcement.Key.StartsWith(AdventureCallout.BackstoryPrefix, StringComparison.Ordinal);
}

public class AMidpointBeatNudgesTheBackstoryOnceTests : BackstoryNudgeTestBase
{
    [Fact]
    public void TheNudgeFollowsTheMidpointBeat()
    {
        var said = Through(Wired(), 3);

        Assert.Equal(
            ["adventure-ack.the-lantern-route.2", "adventure.the-lantern-route.2", "adventure-backstory.the-lantern-route.2"],
            said.Select(announcement => announcement.Key));
        Assert.Equal(AdventureCallout.BackstoryLine, said[^1].Text);
    }

    [Fact]
    public void AnOrdinaryBeatGetsNoNudge()
    {
        Assert.DoesNotContain(Through(Wired(), 2), IsNudge);
    }

    [Fact]
    public void ALaterBeatDoesNotRepeatIt()
    {
        var callout = Wired();
        Through(callout, 3);

        Assert.Empty(callout.Examine(new CalloutContext(
            Accepted.AddHours(1), false, new CommanderGameState(new CommanderIdentity("F1", "Tester")), GameStatus.Unknown, NavRoute.None, [])));
    }
}

public class AnAllIsLostBeatNudgesTheBackstoryTests : BackstoryNudgeTestBase
{
    [Fact]
    public void TheNudgeFollowsTheAllIsLostBeat()
    {
        Assert.Contains(Through(Wired(), 4), IsNudge);
    }

    [Fact]
    public void TheFunctionWordsAreMatchedWithoutRegardToCase()
    {
        Assert.True(AdventureStanding.IsTurning("All is lost"));
        Assert.True(AdventureStanding.IsTurning("the TURN"));
        Assert.False(AdventureStanding.IsTurning("finale"));
        Assert.False(AdventureStanding.IsTurning(null));
    }
}

public class NoNudgeWithoutABackstoryTests : BackstoryNudgeTestBase
{
    [Fact]
    public void ABlankBackstoryGetsNone()
    {
        Assert.DoesNotContain(Through(Wired(backstory: false), 3), IsNudge);
    }
}

public class NoNudgeWhenTheBeatWasDroppedTests : BackstoryNudgeTestBase
{
    [Fact]
    public void ABeatDroppedInDangerTakesItsNudgeWithIt()
    {
        var danger = GameStatus.Unknown with { Flags = StatusFlags.InMainShip | StatusFlags.InDanger, ReadAt = Accepted.AddMinutes(3) };

        var said = Through(Wired(), 3, danger);

        Assert.DoesNotContain(said, announcement => announcement.Key == "adventure.the-lantern-route.2");
        Assert.DoesNotContain(said, IsNudge);
    }
}

public class NoNudgeWhenItsSwitchIsOffTests : BackstoryNudgeTestBase
{
    [Fact]
    public void TheSwitchSilencesIt()
    {
        Assert.DoesNotContain(Through(Wired(switchedOn: false), 3), IsNudge);
    }
}
