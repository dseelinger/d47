using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Of the cast aboard, as many as the Commander's hull seats are heard in the cockpit and the rest over comms, without the
/// story's record changing (#1760).
/// </summary>
public sealed class ACastMemberWithNoSeatIsHeardOverCommsTests
{
    private const string Sidewinder = "sidewinder";
    private const string Cobra = "cobramkiii";
    private const string Anaconda = "anaconda";

    private static StorySpeaker Member(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Who = "A passenger.",
        Provider = StorySpeaker.Kokoro,
        Voice = "bm_lewis",
        Link = 0.3,
        Aboard = [new(null, null)],
    };

    private static readonly StorySecret Two = Secret with { Cast = [.. Secret.Cast, Member("first", "Vale"), Member("second", "Orrin")] };

    private static readonly StorySecret One = Secret with { Cast = [.. Secret.Cast, Member("first", "Vale")] };

    private static readonly Story Picked = new() { Id = Id, Title = Card.Title, PublicLayer = "public" };

    [Fact]
    public void InATwoSeatHullTheFirstInSeatOrderIsInTheCockpitAndTheSecondOnCommsWithTheirLink()
    {
        var aboard = StoryAboard.Seated(Two, ["second", "first"], ["second", "first"], Cobra);

        Assert.Equal(["first"], aboard);
        Assert.Null(StoryVoices.Of("first", Two, null, null, aboard: aboard).Pinned!.Link);
        Assert.Equal(0.3, StoryVoices.Of("second", Two, null, null, aboard: aboard).Pinned!.Link);
    }

    [Fact]
    public void MembersAboardAsTheChapterOpensAreSeatedBeforeThoseWhoBoardDuringIt() =>
        Assert.Equal(["second"], StoryAboard.Seated(Two, ["first", "second"], ["second"], Cobra));

    [Fact]
    public void InAFourSeatHullBothAreInTheCockpit() =>
        Assert.Equal(["first", "second"], StoryAboard.Seated(Two, ["first", "second"], ["first", "second"], Anaconda));

    [Fact]
    public void WithAnUnknownHullBothAreInTheCockpit()
    {
        Assert.Equal(["first", "second"], StoryAboard.Seated(Two, ["first", "second"], ["first", "second"], "nosuchhull"));
        Assert.Equal(["first", "second"], StoryAboard.Seated(Two, ["first", "second"], ["first", "second"], null));
    }

    [Fact]
    public void AStoryWithNoMembersAboardIsUnaffected()
    {
        Assert.Empty(StoryAboard.Seated(Secret, [], [], Sidewinder));
        Assert.Empty(StoryAboard.Seatless(Secret, [], [], Sidewinder));
    }

    [Fact]
    public async Task AMoveToASmallerHullPutsAMemberOnCommsAndChangesNothingSaved()
    {
        using var fixtures = new StoryFixtures(Scripted(), Two);
        var ship = Cobra;
        fixtures.Director.Game = () => Loaded(0, ship);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var before = fixtures.Stories.Current("F1")!;
        Assert.Null(fixtures.Director.LineVoice("F1", "first")!.Pinned!.Link);
        Assert.Equal(0.3, fixtures.Director.LineVoice("F1", "second")!.Pinned!.Link);

        ship = Sidewinder;

        Assert.Equal(0.3, fixtures.Director.LineVoice("F1", "first")!.Pinned!.Link);
        Assert.Equal(0.3, fixtures.Director.LineVoice("F1", "second")!.Pinned!.Link);

        var after = fixtures.Stories.Current("F1")!;
        Assert.Equal(before.Switched, after.Switched);
        Assert.Equal(before.AwaitingSeat, after.AwaitingSeat);
        Assert.Equal(["first", "second"], StoryAboard.BySpan(Two, after));
        Assert.Equal(["first", "second"], fixtures.Director.CastAboard("F1"));

        ship = Anaconda;

        Assert.Null(fixtures.Director.LineVoice("F1", "first")!.Pinned!.Link);
        Assert.Null(fixtures.Director.LineVoice("F1", "second")!.Pinned!.Link);
    }

    [Fact]
    public async Task TheChapterWriterIsToldAMemberWithNoSeatInAOneSeatHullIsOnComms()
    {
        using var fixtures = new StoryFixtures(Scripted(), One);
        fixtures.Director.Game = () => Loaded(0, Sidewinder);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(["first"], fixtures.Asks[0].Story!.OnComms);
        Assert.Contains(fixtures.Provider.Requests, request => request.Prompt.History[0].Text.Contains(
            "The Commander's ship has no seat for Vale: write their lines as heard over comms.", StringComparison.Ordinal));
        Assert.DoesNotContain(fixtures.Provider.Requests, request => request.Prompt.History[0].Text.Contains(
            "Vale is in the cockpit", StringComparison.Ordinal));
    }

    private static RoundScriptedLlmProvider Scripted() => new(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
        RoundScriptedLlmProvider.Saying(NothingObjected));
}
