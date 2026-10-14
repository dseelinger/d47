using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.AStrangerIsIntroducedBeforeTheyFirstSpeakTests;

namespace D47.Core.Tests.Stories;

/// <summary>A cast member is introduced once a story: not again later in the chapter, in a rewrite, or once marked.</summary>
public sealed class AnIntroducedMemberIsNotIntroducedAgainTests
{
    private const string Caller = "caller";

    private static AdventureBeat Beat(string title, params AdventureLine[] lines) => new()
    {
        Title = title,
        Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1, System = "Ossen's Lantern" },
        Lines = lines,
    };

    private static AdventureLine Said(string text, string speaker) => new() { Text = text, Speaker = speaker };

    private static readonly Adventure Chapter = new()
    {
        Key = "yard-paint",
        Name = "Yard Paint",
        OpeningLines = [Said("Anyone. Please.", Caller)],
        Beats =
        [
            Beat("The Lantern", Said("Still here.", Caller)),
            Beat("The Anchorage", Said("You came.", Caller)),
        ],
    };

    private static int Introductions(Adventure chapter) =>
        chapter.OpeningLines.Concat(chapter.Beats.SelectMany(beat => beat.Lines)).Count(line => line.Introduces is not null);

    [Fact]
    public void OnlyTheMembersFirstLineInTheChapterIsIntroduced()
    {
        var introduced = StoryIntroductions.Introduce(Chapter, Running(), WithRoles, null);

        Assert.Equal(1, Introductions(introduced));
        Assert.Equal(Caller, introduced.OpeningLines[0].Introduces);
    }

    [Fact]
    public void AMemberMarkedIntroducedIsNotIntroducedInALaterChapter()
    {
        var introduced = StoryIntroductions.Introduce(Chapter, Running(Caller), WithRoles, null);

        Assert.Equal(0, Introductions(introduced));
        Assert.Null(StoryIntroductions.Line(Running(Caller), WithRoles, null, Caller));
    }

    [Fact]
    public void ARewriteDoesNotIntroduceAMemberTheKeptBeatsIntroduced()
    {
        var once = StoryIntroductions.Introduce(Chapter, Running(), WithRoles, null);
        var rewritten = once with { Beats = [once.Beats[0], Beat("A New Way", Said("Not that way.", Caller))] };

        var again = StoryIntroductions.Introduce(rewritten, Running(), WithRoles, null, from: 1);

        Assert.Equal(1, Introductions(again));
        Assert.Single(again.Beats[1].Lines);
    }

    [Fact]
    public void ARewriteIntroducesAMemberWhoHasNotSpokenYet()
    {
        var rewritten = Chapter with { OpeningLines = [], Beats = [Beat("The Lantern", Said("Scoop here.", StorySpeaker.Ship)), Beat("A New Way", Said("Not that way.", Caller))] };

        var again = StoryIntroductions.Introduce(rewritten, Running(), WithRoles, null, from: 1);

        Assert.Equal(Caller, again.Beats[1].Lines[0].Introduces);
        Assert.Single(again.Beats[0].Lines);
    }

    [Fact]
    public async Task ChapterOneLeavesTheIntroductionToTheStorysOpening()
    {
        var beats = """
            {"opening": "A voice on the open channel.", "openingSpeaker": "narrator", "reply": "Here it is.", "beats": [
              {"title": "Yard Paint", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "lines": [{"text": "Still here.", "speaker": "caller"}]},
              {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "Scan it."}
            ]}
            """;

        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(StoryFixtures.Spine), RoundScriptedLlmProvider.Saying(beats)),
            WithRoles with { Opening = [new StoryLine("Anyone. Please.", Caller)] });

        Assert.Null(await fixtures.Director.PickAsync("F1", StoryFixtures.Id, StoryFixtures.Now, CancellationToken.None));

        var chapter = fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;

        Assert.Equal(0, Introductions(chapter));
        Assert.NotNull(fixtures.Director.Introduction("F1", Caller));
    }

    [Fact]
    public void WalkingAnIntroducedChapterAgainAddsNothing()
    {
        var once = StoryIntroductions.Introduce(Chapter, Running(), WithRoles, null);

        Assert.Equal(once.OpeningLines, StoryIntroductions.Introduce(once, Running(), WithRoles, null).OpeningLines);
    }
}
