using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Persona;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// The running story takes a cast reaction once chapter one has begun, from its primary cast only, and once a reaction
/// is spoken takes no other until the next play session.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheStoryOpensOneReactionASessionTests
{
    private static readonly StorySecret WithAPrimaryMember = Secret with
    {
        Cast =
        [
            Secret.Cast[0] with { Primary = true },
            new StorySpeaker { Id = "clerk", Name = "Ama", Who = "A station clerk.", Provider = StorySpeaker.Kokoro, Voice = "af_heart" },
        ],
    };

    private static JournalEvent LoadGame(DateTimeOffset at) => BeaconFixture.Event(
        $$"""{ "timestamp":"{{at.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"F1", "Commander":"Test" }""");

    private static StoryFixtures Picking() => new(
        new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
        WithAPrimaryMember);

    [Fact]
    public async Task OnlyThePrimaryCastMayReactOnceChapterOneHasBegun()
    {
        using var fixtures = Picking();

        Assert.Null(fixtures.Director.ReactionWindow("F1"));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        fixtures.Director.TakeOpening("F1");
        fixtures.Director.TakeNarratedScan("F1");

        var window = fixtures.Director.ReactionWindow("F1");

        Assert.Equal(new StoryReactionWindow("F1", Id, 0, false), window);
        Assert.Equal([new StoryReactor("dock-hand", "Ren", "A tired dock hand, short words.")], fixtures.Director.ReactionCast("F1", window!.Due));
    }

    [Fact]
    public async Task ASpokenReactionClosesTheSessionUntilTheNextLoadGame()
    {
        using var fixtures = Picking();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Director.TakeOpening("F1");
        fixtures.Director.TakeNarratedScan("F1");

        var due = fixtures.Director.ReactionWindow("F1")!.Due;

        fixtures.Director.ReactionGiven("F1", due);

        Assert.True(fixtures.Director.ReactionWindow("F1")!.Reacted);
        Assert.Empty(fixtures.Director.ReactionCast("F1", due));

        fixtures.Director.Observe(LoadGame(Now.AddHours(1)), "F1");

        Assert.Equal(new StoryReactionWindow("F1", Id, 1, false), fixtures.Director.ReactionWindow("F1"));
        Assert.Empty(fixtures.Director.ReactionCast("F1", due));
        Assert.Single(fixtures.Director.ReactionCast("F1", new StoryReactionDue(Id, 1)));
    }

    [Fact]
    public async Task AStoryWithNoPrimaryCastTakesNoReaction()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Director.TakeOpening("F1");
        fixtures.Director.TakeNarratedScan("F1");

        Assert.Null(fixtures.Director.ReactionWindow("F1"));
    }

    [Fact]
    public async Task ASwitchedOffStoryTakesNoReaction()
    {
        using var fixtures = Picking();

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Director.TakeOpening("F1");
        fixtures.Director.TakeNarratedScan("F1");

        Assert.Null(fixtures.Director.SetOn("F1", false, Now.AddMinutes(1)));

        Assert.Null(fixtures.Director.ReactionWindow("F1"));
    }
}
