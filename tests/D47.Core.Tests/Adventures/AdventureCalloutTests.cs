using D47.Core.Audio;
using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A beat, said when it is reached — after a settle, never mid-danger, never from the priming
/// backlog.</summary>
public class AdventureCalloutTests
{
    private static readonly string AdventuresFile = Path.Combine(MemoryInstall.FakeRoot, "adventures.json");

    private (AdventureBook Book, AdventureCallout Callout) Wired()
    {
        var store = new AdventureStore(AdventuresFile, new MemoryFileSystem(), NullLogger<AdventureStore>.Instance);
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

        // The acknowledgement lands on the tick the beat fired, with no settle behind it — that is the whole
 // of what it is for.
        var ack = Assert.Single(callout.Examine(At(reached, [Jump(Lantern, reached)])));
        Assert.Equal("adventure-ack.the-lantern-route.0", ack.Key);
        var stock = Enumerable.Range(0, AdventureAcks.Count).Select(AdventureAcks.Pick).ToList();
        Assert.Contains(stock, line => ack.Text.StartsWith(line, StringComparison.Ordinal));
        Assert.EndsWith("You arrived at Ossen's Lantern.", ack.Text, StringComparison.Ordinal);

        Assert.Empty(callout.Examine(At(reached.AddSeconds(10), [])));

        var spoken = callout.Examine(At(reached.AddSeconds(20), [])).ToList();
        var said = spoken[0];

        Assert.Equal("adventure.the-lantern-route.0", said.Key);
        Assert.Equal("Scoop here.", said.Text);
        Assert.Equal(0, said.Variant);
        Assert.Equal(CalloutUrgency.Routine, said.Urgency);

        // Where to go next, and for a scan how, is its own announcement in the Narrator's voice.
        var handOff = Assert.Single(spoken.Skip(1));
        Assert.Equal("adventure-handoff.the-lantern-route.0", handOff.Key);
        Assert.Equal(LanternHandOff, handOff.Text);
        Assert.Equal(VoiceRole.Narrator, handOff.Voice);
    }

    [Fact]
    public void EachBeatHandsOverToTheNextAndTheLastToNothing()
    {
        var (book, callout) = Wired();
        book.Abandon("F1", "the-lantern-route", Accepted);
        book.Begin("F1", "the-lantern-route", Accepted.AddMinutes(1));

        var first = callout.Examine(At(Accepted.AddMinutes(1), [])).ToList();
        Assert.Equal("Beacons cost money. Somebody is paying.", first[0].Text);
        Assert.Equal("Next: arrive at Ossen's Lantern.", Assert.Single(first.Skip(1)).Text);

        var from = Accepted.AddMinutes(2);
        callout.Examine(At(from, WholeRoute(from))).ToList();
        var said = callout.Examine(At(from.AddSeconds(20), [])).ToList();

        var handOffs = said.Where(a => a.Key.StartsWith(AdventureCallout.HandOffPrefix, StringComparison.Ordinal)).ToList();

        Assert.Equal(9, said.Count);
        Assert.Equal(4, handOffs.Count);
        Assert.All(handOffs, a => Assert.Equal(VoiceRole.Narrator, a.Voice));
        Assert.Equal("Next: dock at Maren Anchorage in Dyson's Hollow.", handOffs[1].Text);
        Assert.Equal("Next: land on Veyl 3 c in Cairn of Veyl.", handOffs[2].Text);
        Assert.Equal("Next: arrive at Tavell's Reach.", handOffs[3].Text);
        Assert.Equal("Eleven months left.", said[^1].Text);
        Assert.DoesNotContain(said.Where(a => a.Voice != VoiceRole.Narrator), a => a.Text.Contains("Next:", StringComparison.Ordinal));
    }

    [Fact]
    public void TheOpeningDoesNotWait()
    {
        var (book, callout) = Wired();
        book.Abandon("F1", "the-lantern-route", Accepted);
        book.Begin("F1", "the-lantern-route", Accepted.AddMinutes(1));

        var said = callout.Examine(At(Accepted.AddMinutes(1), [])).First();

        Assert.Equal("adventure.the-lantern-route.opening", said.Key);
        Assert.Equal(-1, said.Variant);
    }

    [Fact]
    public void AHeldOpeningWaitsUntilTheHoldLifts()
    {
        var (book, _) = Wired();
        var held = true;
        var callout = new AdventureCallout(book) { Held = commander => held && commander == "F1" };
        book.Abandon("F1", "the-lantern-route", Accepted);
        book.Begin("F1", "the-lantern-route", Accepted.AddMinutes(1));

        Assert.Empty(callout.Examine(At(Accepted.AddMinutes(1), [])));
        Assert.Empty(callout.Examine(At(Accepted.AddMinutes(2), [])));

        held = false;

        Assert.Equal("adventure.the-lantern-route.opening", callout.Examine(At(Accepted.AddMinutes(3), [])).First().Key);
    }

    private static readonly string LanternHandOff =
        "Next: scan The Quiet Field A 2 in The Quiet Field — the ship's own scanner from supercruise does it, or a close pass; no surface scanner is needed, and simply going there counts if you have scanned it before.";

    private static GameStatus Interdicted(DateTimeOffset at) => GameStatus.Unknown with
    {
        Flags = StatusFlags.InMainShip | StatusFlags.BeingInterdicted,
        ReadAt = at,
    };

    [Fact]
    public void ABeatDueMidDangerSaysOnlyItsHandOffOnceClear()
    {
        var (book, callout) = Wired();
        var reached = Accepted.AddMinutes(1);

        callout.Examine(At(reached, [Jump(Lantern, reached)])).ToList();

        Assert.Empty(callout.Examine(At(reached.AddSeconds(30), [], Interdicted(reached))));
        Assert.Empty(callout.Examine(At(reached.AddSeconds(40), [], Interdicted(reached))));

        var said = Assert.Single(callout.Examine(At(reached.AddMinutes(5), [])));

        Assert.Equal("adventure-handoff.the-lantern-route.0", said.Key);
        Assert.Equal(LanternHandOff, said.Text);
        Assert.Equal(VoiceRole.Narrator, said.Voice);
        Assert.Empty(callout.Examine(At(reached.AddMinutes(6), [])));

        // Still in the context: the fold moved.
        Assert.Single(book.Standing("F1", "the-lantern-route")!.Fired);
    }

    [Fact]
    public void ANewerBeatReachedWhileAnEarlierOneIsHeldSaysOnlyTheNewerHandOff()
    {
        var (_, callout) = Wired();
        var from = Accepted.AddMinutes(1);
        var route = WholeRoute(Accepted);

        callout.Examine(At(from, [route[0], route[1]])).ToList();

        Assert.Empty(callout.Examine(At(from.AddSeconds(30), [], Interdicted(from))));

        var said = Assert.Single(callout.Examine(At(from.AddMinutes(2), [])));

        Assert.Equal("adventure-handoff.the-lantern-route.1", said.Key);
        Assert.Equal("Next: dock at Maren Anchorage in Dyson's Hollow.", said.Text);
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

        Assert.Equal([0, 1, 2], said.Where(a => a.Variant is not null).Select(a => a.Variant));
    }
}
