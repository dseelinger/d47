using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Before a cast member with a role first speaks in a story, the narrator says who they are: stored in a written chapter
/// before the member's first line, and owed before an opening, scan or clue line until it has been said.
/// </summary>
public sealed class AStrangerIsIntroducedBeforeTheyFirstSpeakTests
{
    private const string Caller = "caller";

    private const string Introduction = "The caller is a pilot stranded past the Pleiades.";

    internal static readonly StorySecret WithRoles = Secret with
    {
        Cast =
        [
            .. Secret.Cast,
            new StorySpeaker
            {
                Id = Caller,
                Name = "The caller",
                Who = "A frightened pilot on an open channel.",
                Role = "a pilot stranded past the Pleiades",
                Provider = StorySpeaker.Kokoro,
                Voice = "bm_lewis",
            },
        ],
    };

    private const string BeatsWithTheCaller = """
        {"opening": "A voice on the open channel.", "openingSpeaker": "narrator", "reply": "Here it is.", "beats": [
          {"title": "Yard Paint", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "lines": [
            {"text": "Anyone. Please.", "speaker": "caller"},
            {"text": "The channel hisses, and the yard lights stay on.", "speaker": "narrator"},
            {"text": "Still here. Still waiting.", "speaker": "caller"}
          ]},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "lines": [{"text": "You came.", "speaker": "caller"}]},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "Scan it."}
        ]}
        """;

    private static StoryFixtures Picked()
    {
        var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsWithTheCaller)),
            WithRoles);

        Assert.Null(fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None).GetAwaiter().GetResult());
        return fixtures;
    }

    [Fact]
    public void AWrittenChapterHoldsTheIntroductionJustBeforeTheMembersFirstLine()
    {
        using var fixtures = Picked();

        var chapter = fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;
        var lines = chapter.Beats[0].Lines;

        Assert.Equal(4, lines.Count);
        Assert.Equal(new AdventureLine { Text = Introduction, Speaker = StorySpeaker.Narrator, Introduces = Caller }, lines[0]);
        Assert.Equal("Anyone. Please.", lines[1].Text);
        Assert.Equal("Still here. Still waiting.", lines[3].Text);
        Assert.All(chapter.Beats.Skip(1).SelectMany(beat => beat.Lines), line => Assert.Null(line.Introduces));
        Assert.Equal(VoiceRole.Narrator, fixtures.Director.LineVoice("F1", chapter.SpeakerOf(0, 0))!.Role);
    }

    [Fact]
    public void TheStoredIntroductionSurvivesAReload()
    {
        using var fixtures = Picked();

        var key = fixtures.Stories.Current("F1")!.CurrentChapter!;
        var store = new AdventureStore(fixtures.AdventuresPath, fixtures.Files, NullLogger<AdventureStore>.Instance);

        Assert.True(store.Poll());

        var reloaded = store.Find("F1", key)!;

        Assert.Equal(Caller, reloaded.Beats[0].Lines[0].Introduces);
        Assert.Null(reloaded.Beats[0].Lines[1].Introduces);
    }

    [Fact]
    public void AnOpeningOrClueLineIsOwedTheIntroductionUntilItIsSaid()
    {
        using var fixtures = Picked();

        Assert.Equal(Introduction, fixtures.Director.Introduction("F1", Caller));

        fixtures.Director.Introduced("F1", Id, Caller);

        Assert.Null(fixtures.Director.Introduction("F1", Caller));
        Assert.Equal([Caller], fixtures.Stories.Current("F1")!.Introduced);
    }

    [Fact]
    public void ABeatAtTheWritersCapStillTakesTheIntroduction()
    {
        var beat = new AdventureBeat
        {
            Title = "Yard Paint",
            Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1, System = "Ossen's Lantern" },
            Lines =
            [
                new AdventureLine { Text = "The yard is dark.", Speaker = StorySpeaker.Narrator },
                new AdventureLine { Text = "Anyone. Please.", Speaker = Caller },
                new AdventureLine { Text = "Scoop here.", Speaker = StorySpeaker.Ship },
            ],
        };
        var chapter = new Adventure { Key = "yard-paint", Name = "Yard Paint", OpeningLines = beat.Lines, Beats = [beat] };

        var introduced = StoryIntroductions.Introduce(chapter, Running(), WithRoles, gender: null);

        Assert.Equal(AdventureLimits.MaxLinesPerBeat + 1, introduced.OpeningLines.Count);
        Assert.Equal(AdventureLimits.MaxLinesPerBeat, introduced.Beats[0].Lines.Count);
        Assert.Empty(AdventureValidation.Problems(introduced));
    }

    [Fact]
    public void AStoryAlreadyRunningStartsWithNobodyIntroduced()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(MemoryInstall.FakeRoot, "d47-introductions", Guid.NewGuid().ToString("N"), "story.json");

        files.CreateFolder(Path.GetDirectoryName(path)!);
        files.WriteText(path, $$"""
            {"commanders": [{"frontierId": "F1", "stories": [{"id": "{{Id}}", "title": "The Test Story", "publicLayer": "x", "chapters": ["one"]}]}]}
            """);

        var story = StoryStore.Open(path, files, NullLogger<StoryStore>.Instance).Current("F1");

        Assert.NotNull(story);
        Assert.Empty(story.Introduced);
        Assert.Equal(Introduction, StoryIntroductions.Line(story, WithRoles, null, Caller));
    }

    internal static Story Running(params string[] introduced) => new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        Chapters = ["one"],
        Introduced = introduced,
    };
}
