using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A hand-off is its own announcement in the Narrator's voice: the reason, then "Next: …".</summary>
public sealed class TheNarratorSaysEveryHandOffTests
{
    private const string Reason = "The Commander goes to read the column the Sentinel wrote.";
    private const string Key = "the-lantern-route";

    private static readonly AdventureTrigger Arrive = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };
    private static readonly AdventureTrigger Dock = new() { Kind = TriggerKind.Dock, MarketId = Anchorage, System = "Dyson's Hollow", Station = "Maren Anchorage" };
    private static readonly AdventureTrigger Bounty = new() { Kind = TriggerKind.Bounty, Count = 1 };

    private static (AdventureBook Book, AdventureCallout Callout) Wired(Adventure adventure)
    {
        var store = new AdventureStore(Path.Combine(MemoryInstall.FakeRoot, "narrated-hand-offs", "adventures.json"), new MemoryFileSystem(), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write("F1", adventure);
        book.CatchUp([]);
        return (book, new AdventureCallout(book) { Settle = TimeSpan.FromSeconds(20) });
    }

    private static Adventure WithReasons()
    {
        var route = LanternRoute(Accepted);
        return route with { Beats = [route.Beats[0], route.Beats[1] with { Reason = Reason }, .. route.Beats.Skip(2)] };
    }

    private static CalloutContext At(DateTimeOffset now, IReadOnlyList<JournalEvent> events, GameStatus? status = null) =>
        new(now, false, new CommanderGameState(new CommanderIdentity("F1", "Tester")), status ?? GameStatus.Unknown, NavRoute.None, events);

    [Fact]
    public void ABeatEndingOnTheShipsLineSaysThatLineAloneAndThenTheNarratorsHandOff()
    {
        var (_, callout) = Wired(WithReasons());
        var reached = Accepted.AddMinutes(1);

        callout.Examine(At(reached, [Jump(Lantern, reached)])).ToList();
        var said = callout.Examine(At(reached.AddSeconds(20), [])).ToList();

        Assert.Equal(2, said.Count);
        Assert.Equal("Scoop here.", said[0].Text);
        Assert.Equal(VoiceRole.ShipAi, said[0].Voice);
        Assert.Equal($"{AdventureCallout.HandOffPrefix}{Key}.0", said[1].Key);
        Assert.StartsWith($"{Reason} Next: scan ", said[1].Text, StringComparison.Ordinal);
        Assert.Equal(VoiceRole.Narrator, said[1].Voice);
    }

    [Fact]
    public void AStepHandOffIsTheNarratorsReasonThenNext()
    {
        var route = LanternRoute(Accepted);
        var steps = route.Beats[0] with
        {
            Steps = [new BeatStep { Trigger = Arrive }, new BeatStep { Trigger = Bounty, Reason = Reason }, new BeatStep { Trigger = Dock }],
        };
        var (_, callout) = Wired(route with { Beats = [steps, .. route.Beats.Skip(1)] });
        var reached = Accepted.AddMinutes(1);

        callout.Examine(At(reached, [Jump(Lantern, reached)])).ToList();
        var said = Assert.Single(callout.Examine(At(reached.AddSeconds(30), [])));

        Assert.StartsWith(AdventureCallout.StepPrefix, said.Key, StringComparison.Ordinal);
        Assert.StartsWith($"{Reason} Next: ", said.Text, StringComparison.Ordinal);
        Assert.Equal(VoiceRole.Narrator, said.Voice);
    }

    [Fact]
    public void AHeldBeatsHandOffIsTheNarratorsOnceClear()
    {
        var (_, callout) = Wired(WithReasons());
        var reached = Accepted.AddMinutes(1);
        var interdicted = GameStatus.Unknown with { Flags = StatusFlags.InMainShip | StatusFlags.BeingInterdicted, ReadAt = reached };

        callout.Examine(At(reached, [Jump(Lantern, reached)])).ToList();
        Assert.Empty(callout.Examine(At(reached.AddSeconds(30), [], interdicted)));

        var said = Assert.Single(callout.Examine(At(reached.AddMinutes(5), [])));

        Assert.Equal($"{AdventureCallout.HandOffPrefix}{Key}.0", said.Key);
        Assert.StartsWith($"{Reason} Next: scan ", said.Text, StringComparison.Ordinal);
        Assert.Equal(VoiceRole.Narrator, said.Voice);
    }

    [Fact]
    public void NoAnnouncementCarriesAShipLineFollowedByNext()
    {
        var (book, callout) = Wired(WithReasons());
        book.Abandon("F1", Key, Accepted);
        book.Begin("F1", Key, Accepted.AddMinutes(1));

        var said = callout.Examine(At(Accepted.AddMinutes(1), [])).ToList();
        var from = Accepted.AddMinutes(2);
        callout.Examine(At(from, WholeRoute(from))).ToList();
        said.AddRange(callout.Examine(At(from.AddSeconds(20), [])));

        Assert.DoesNotContain(said.Where(a => a.Voice != VoiceRole.Narrator), a => a.Text.Contains("Next:", StringComparison.Ordinal));
    }

    [Fact]
    public void AHandOffGetsNoBriefSoItIsSaidAsWritten()
    {
        var handOff = new Announcement($"{AdventureCallout.HandOffPrefix}{Key}.0", $"{Reason} Next: dock.") { Voice = VoiceRole.Narrator };
        var step = new Announcement($"{AdventureCallout.StepPrefix}{Key}.0.0", $"{Reason} Next: dock.") { Voice = VoiceRole.Narrator };

        Assert.Null(FlavourBriefs.For(handOff, personalityEnabled: true));
        Assert.Null(FlavourBriefs.For(step, personalityEnabled: true));
    }

    [Fact]
    public void TheChapterPromptsReasonRuleAsksForThirdPersonNarration()
    {
        using var stream = typeof(TheNarratorSaysEveryHandOffTests).Assembly.GetManifestResourceStream("Prompts.lines-short-story.txt")!;
        var prompt = new StreamReader(stream).ReadToEnd();

        Assert.Contains("write it in the third person, about the Commander, never as \"I\", \"me\" or \"we\"", prompt, StringComparison.Ordinal);
        Assert.Contains("never speaks to the Commander, and never offers or asks.", prompt, StringComparison.Ordinal);
    }
}
