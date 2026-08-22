using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A beat, said when it is reached — after a settle, never mid-danger, never from the priming
/// backlog (list.md Phase 47, "The ship's AI tells it, and the authored beat is the floor").
/// </summary>
public class AdventureCalloutTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-adventure-callout", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private (AdventureBook Book, AdventureCallout Callout) Wired()
    {
        var store = new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write("F1", LanternRoute(Accepted));
        book.CatchUp([]);
        return (book, new AdventureCallout(book) { Settle = TimeSpan.FromSeconds(20) });
    }

    private static CommanderGameState State() => new(new CommanderIdentity("F1", "Tester"));

    private static CalloutContext At(DateTimeOffset now, IReadOnlyList<JournalEvent> events, GameStatus? status = null, bool priming = false) =>
        new(now, priming, State(), status ?? GameStatus.Unknown, NavRoute.None, events);

    [Fact]
    public void ABeatWaitsOutTheSettleAndThenSpeaksTheAuthoredLine()
    {
        var (_, callout) = Wired();
        var reached = Accepted.AddMinutes(1);

        Assert.Empty(callout.Examine(At(reached, [Jump(Lantern, reached)])));
        Assert.Empty(callout.Examine(At(reached.AddSeconds(10), [])));

        var said = Assert.Single(callout.Examine(At(reached.AddSeconds(20), [])));

        Assert.Equal("adventure.the-lantern-route.0", said.Key);

        // The line, then where to go next — and for a scan, how, because "scan X" sends a
        // Commander looking for a surface scanner they do not need.
        Assert.Equal(
            "Scoop here. Next: scan The Quiet Field A 2 in The Quiet Field — the ship's own scanner from supercruise does it, or a close pass; no surface scanner is needed.",
            said.Text);
        Assert.Equal(0, said.Variant);
        Assert.Equal(CalloutUrgency.Routine, said.Urgency);
    }

    /// <summary>
    /// Every beat hands over to the next, the opening to the first, and the last to nothing: the
    /// ending is the ending. The first story flown left the Commander asking "now what?" after every
    /// beat (2026-08-22), and the next place is already in the context and on the reading level, so
    /// saying it spoils nothing.
    /// </summary>
    [Fact]
    public void EachBeatHandsOverToTheNextAndTheLastToNothing()
    {
        var (book, callout) = Wired();
        book.Abandon("F1", "the-lantern-route", Accepted);
        book.Begin("F1", "the-lantern-route", Accepted.AddMinutes(1));

        var opening = Assert.Single(callout.Examine(At(Accepted.AddMinutes(1), [])));
        Assert.Equal("Beacons cost money. Somebody is paying. Next: arrive at Ossen's Lantern.", opening.Text);

        var from = Accepted.AddMinutes(2);
        callout.Examine(At(from, WholeRoute(from))).ToList();
        var said = callout.Examine(At(from.AddSeconds(20), [])).ToList();

        Assert.Equal(5, said.Count);
        Assert.EndsWith("Next: dock at Maren Anchorage in Dyson's Hollow.", said[1].Text);
        Assert.EndsWith("Next: land on Veyl 3 c in Cairn of Veyl.", said[2].Text);
        Assert.EndsWith("Next: arrive at Tavell's Reach.", said[3].Text);
        Assert.Equal("Eleven months left.", said[4].Text);
    }

    [Fact]
    public void TheOpeningDoesNotWait()
    {
        var (book, callout) = Wired();
        book.Abandon("F1", "the-lantern-route", Accepted);
        book.Begin("F1", "the-lantern-route", Accepted.AddMinutes(1));

        var said = Assert.Single(callout.Examine(At(Accepted.AddMinutes(1), [])));

        Assert.Equal("adventure.the-lantern-route.opening", said.Key);
        Assert.Equal(-1, said.Variant);
    }

    [Fact]
    public void ABeatDueMidDangerIsDroppedAndNotSaidLate()
    {
        var (book, callout) = Wired();
        var reached = Accepted.AddMinutes(1);

        callout.Examine(At(reached, [Jump(Lantern, reached)])).ToList();

        var interdicted = GameStatus.Unknown with
        {
            Flags = StatusFlags.InMainShip | StatusFlags.BeingInterdicted,
            ReadAt = reached,
        };

        Assert.Empty(callout.Examine(At(reached.AddSeconds(30), [], interdicted)));
        Assert.Empty(callout.Examine(At(reached.AddMinutes(5), [])));

        // Still in the context: the fold moved, only the speaking was dropped.
        Assert.Single(book.Standing("F1", "the-lantern-route")!.Fired);
    }

    [Fact]
    public void ThePrimingBacklogFoldsAndSaysNothing()
    {
        var (book, callout) = Wired();
        var reached = Accepted.AddMinutes(1);

        Assert.Empty(callout.Examine(At(reached, [Jump(Lantern, reached)], priming: true)));
        Assert.Empty(callout.Examine(At(reached.AddMinutes(1), [])));
        Assert.Single(book.Standing("F1", "the-lantern-route")!.Fired);
    }

    [Fact]
    public void ThreeBeatsInAMinuteAreThreeLinesButOnlyAfterEachSettles()
    {
        var (_, callout) = Wired();
        var from = Accepted.AddMinutes(1);
        var route = WholeRoute(Accepted);

        callout.Examine(At(from, [route[0], route[1], route[2]])).ToList();

        var said = callout.Examine(At(from.AddSeconds(20), [])).ToList();

        Assert.Equal([0, 1, 2], said.Select(a => a.Variant));
    }
}
