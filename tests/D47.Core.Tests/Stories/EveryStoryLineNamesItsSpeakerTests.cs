using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Messages;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Every story line is spoken by its speaker: the ship as the core aboard, or the narrator while that core is stock; the
/// narrator; or a cast member in its pinned voice and, with versions, the Commander's version (#714).
/// </summary>
public sealed class EveryStoryLineNamesItsSpeakerTests
{
    /// <summary><see cref="Secret"/> with a caller in the Commander's own voice and harrow over a weak link.</summary>
    private static readonly StorySecret Cast = Secret with
    {
        Cast =
        [
            .. Secret.Cast,
            new StorySpeaker { Id = "caller", Name = "The caller", Who = "A frightened pilot on an open channel.", Provider = StorySpeaker.Chatterbox, Voice = StorySpeaker.Own },
            new StorySpeaker
            {
                Id = "harrow",
                Name = "Harrow",
                Who = "A broker, never in a hurry.",
                Provider = StorySpeaker.Kokoro,
                Voice = "bm_lewis",
                Link = 0.3,
                Effects = [new StorySpeakerEffect("glitch", 4)],
            },
        ],
    };

    private const string BeatsWithSpeakers = """
        {"opening": "A voice on the open channel.", "openingSpeaker": "narrator", "reply": "Here it is.", "beats": [
          {"title": "The Mayday", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Anyone. Please.", "speaker": "caller"},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name.", "speaker": "ship"},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "Scan it."}
        ]}
        """;

    private const string BeatsWithAStranger = """
        {"opening": "Factory settings, holding.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here.", "speaker": "stranger"},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "Scan it."}
        ]}
        """;

    [Fact]
    public void TheShipsLineIsTheCoreAboards()
    {
        var voice = StoryVoices.Of(StorySpeaker.Ship, Secret, gender: null, PersonaCatalog.Kex);

        Assert.Equal(VoiceRole.ShipAi, voice.Role);
        Assert.Equal(PersonaCatalog.Kex.Id, voice.From);
        Assert.Null(voice.Pinned);
    }

    [Fact]
    public void WithAStockCoreAboardTheShipsLineIsTheNarrators()
    {
        var voice = StoryVoices.Of(StorySpeaker.Ship, Secret, gender: null, PersonaCatalog.Covas);

        Assert.Equal(VoiceRole.Narrator, voice.Role);
        Assert.Equal(MessageStore.Narrator, voice.From);
    }

    [Fact]
    public void TheNarratorsLineIsTheNarratorsWhateverTheCore()
    {
        Assert.Equal(VoiceRole.Narrator, StoryVoices.Of(StorySpeaker.Narrator, Secret, null, PersonaCatalog.Kex).Role);
        Assert.Equal(VoiceRole.Narrator, StoryVoices.Of(StorySpeaker.Narrator, Secret, null, PersonaCatalog.Covas).Role);
    }

    [Fact]
    public void ACastMembersLineIsPinnedToItsProviderAndVoiceAndPostedFromItsName()
    {
        var voice = StoryVoices.Of("dock-hand", Secret, null, PersonaCatalog.Covas);

        Assert.Equal("Ren", voice.From);
        Assert.Equal(new PinnedVoice(StorySpeaker.Kokoro, "bm_george"), voice.Pinned);
        Assert.Equal("A tired dock hand, short words.", voice.Who);
    }

    [Fact]
    public void ThePinnedVoiceCarriesTheMembersLinkAndEffects()
    {
        var pinned = StoryVoices.Of("harrow", Cast, null, PersonaCatalog.Kex).Pinned!;

        Assert.Equal(0.3, pinned.Link);
        Assert.Equal([new StorySpeakerEffect("glitch", 4)], pinned.Effects);
        Assert.NotNull(CastVoice.Treatment(pinned));
        Assert.Null(CastVoice.Treatment(StoryVoices.Of("dock-hand", Cast, null, PersonaCatalog.Kex).Pinned!));
    }

    [Fact]
    public void AMemberWithTwoVersionsSpeaksInTheCommandersVersion()
    {
        var man = StoryVoices.Of("cray", Versioned, CommanderGender.Man, PersonaCatalog.Kex);
        var woman = StoryVoices.Of("cray", Versioned, CommanderGender.Woman, PersonaCatalog.Kex);

        Assert.Equal(("Ellie", new PinnedVoice(StorySpeaker.Kokoro, "af_heart")), (man.From, man.Pinned));
        Assert.Equal(("Ellis", new PinnedVoice(StorySpeaker.Chatterbox, "ellis-sample")), (woman.From, woman.Pinned));
    }

    [Fact]
    public async Task ChangingTheGenderMidStoryChangesTheVoiceFromTheNextLine()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsToTheBeacon), Versioned);
        var gender = CommanderGender.Man;
        fixtures.Director.Gender = () => gender;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Equal("af_heart", fixtures.Director.LineVoice("F1", "cray")!.Pinned!.VoiceId);

        gender = CommanderGender.Woman;

        Assert.Equal("ellis-sample", fixtures.Director.LineVoice("F1", "cray")!.Pinned!.VoiceId);
    }

    [Fact]
    public async Task TheDirectorVoicesEachClueByItsSpeaker()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsToTheBeacon));
        fixtures.Director.Aboard = () => PersonaCatalog.Kex;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(VoiceRole.ShipAi, fixtures.Director.ClueVoice("F1", new StoryClueDue(Id, 0))!.Role);

        fixtures.Stories.Update("F1", Id, story => story with { CluesGiven = 1 });
        Assert.Equal(VoiceRole.Narrator, fixtures.Director.ClueVoice("F1", new StoryClueDue(Id, 1))!.Role);

        fixtures.Stories.Update("F1", Id, story => story with { CluesGiven = 2 });
        Assert.Equal("Ren", fixtures.Director.ClueVoice("F1", new StoryClueDue(Id, 2))!.From);

        // Only the clue the story owes now has a voice.
        Assert.Null(fixtures.Director.ClueVoice("F1", new StoryClueDue(Id, 0)));
    }

    [Fact]
    public async Task AChapterLineCanBeACastMembersAndKeepsItsSpeaker()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsWithSpeakers), Cast);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var chapter = fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;

        Assert.Equal(StorySpeaker.Narrator, chapter.OpeningSpeaker);
        Assert.Equal(["caller", StorySpeaker.Ship, null], chapter.Beats.Select(beat => beat.Speaker));
        Assert.Equal(new PinnedVoice(StorySpeaker.Chatterbox, StorySpeaker.Own), fixtures.Director.LineVoice("F1", "caller")!.Pinned);
    }

    [Fact]
    public async Task AChapterKeepsItsSpeakersWhenItIsReadBackFromDisk()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsWithSpeakers), Cast);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var key = fixtures.Stories.Current("F1")!.CurrentChapter!;
        var reopened = new AdventureStore(fixtures.AdventuresPath, fixtures.Files, Microsoft.Extensions.Logging.Abstractions.NullLogger<AdventureStore>.Instance);
        Assert.True(reopened.Poll());
        var chapter = reopened.Find("F1", key)!;

        Assert.Equal(StorySpeaker.Narrator, chapter.OpeningSpeaker);
        Assert.Equal(["caller", StorySpeaker.Ship, null], chapter.Beats.Select(beat => beat.Speaker));
    }

    [Fact]
    public async Task ASpeakerWrittenInAnotherCaseIsTheStorysOwn()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsWithSpeakers.Replace("\"caller\"", "\"Caller\"", StringComparison.Ordinal)), Cast);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var chapter = fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;

        Assert.Equal("caller", chapter.Beats[0].Speaker);
    }

    [Fact]
    public async Task AChapterLineNamingASpeakerTheStoryDoesNotHaveIsRefused()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsWithAStranger),
            RoundScriptedLlmProvider.Saying(BeatsWithAStranger)));

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.NotNull(refusal);
        Assert.Contains("\"stranger\"", refusal, StringComparison.Ordinal);
    }

    private const string BeatsWhereHarrowNamesHimself = """
        {"opening": "A voice on the open channel.", "openingSpeaker": "narrator", "reply": "Here it is.", "beats": [
          {"title": "The Mayday", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Harrow keeps a quiet office.", "speaker": "harrow"},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name.", "speaker": "ship"},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "Scan it."}
        ]}
        """;

    [Fact]
    public async Task ACastLineThatNamesItsOwnSpeakerIsRefusedThenPassesWhenRewritten()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine),
                RoundScriptedLlmProvider.Saying(BeatsWhereHarrowNamesHimself),
                RoundScriptedLlmProvider.Saying(BeatsWhereHarrowNamesHimself.Replace("Harrow keeps a quiet office.", "I keep a quiet office.", StringComparison.Ordinal))),
            Cast);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var chapter = fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;
        var rewritePrompt = fixtures.Provider.Requests[2].Prompt.History[0].Text;

        Assert.Contains("A line names its own speaker: \"Harrow\" in objective 1", rewritePrompt, StringComparison.Ordinal);
        Assert.Equal("harrow", chapter.Beats[0].Speaker);
        Assert.Equal("I keep a quiet office.", chapter.Beats[0].Line);
    }

    [Fact]
    public async Task ACastLineThatStillNamesItsSpeakerAfterTheRewriteGoesToTheNarrator()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine),
                RoundScriptedLlmProvider.Saying(BeatsWhereHarrowNamesHimself),
                RoundScriptedLlmProvider.Saying(BeatsWhereHarrowNamesHimself)),
            Cast);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var chapter = fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;

        Assert.Equal(StorySpeaker.Narrator, chapter.Beats[0].Speaker);
        Assert.Equal("Harrow keeps a quiet office.", chapter.Beats[0].Line);
    }

    [Fact]
    public async Task ACastNameInsideAnotherWordIsNotSelfNaming()
    {
        using var fixtures = new StoryFixtures(
            Scripted(BeatsWhereHarrowNamesHimself.Replace("Harrow keeps a quiet office.", "Harrowing, isn't it.", StringComparison.Ordinal)),
            Cast);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var chapter = fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;

        Assert.Equal("harrow", chapter.Beats[0].Speaker);
        Assert.Equal(2, fixtures.Provider.Requests.Count);
    }

    [Fact]
    public async Task TheChapterWriterIsToldToWriteCastLinesInTheirOwnWordsAndEveryLinePlainly()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsToTheBeacon));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var prompt = fixtures.Provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("is that person's own words, in the first person", prompt, StringComparison.Ordinal);
        Assert.Contains("Write every line plainly", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheChapterWriterIsToldTheSpeakersAndThatAStockCoreDoesNotTellTheStory()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsToTheBeacon));
        fixtures.Director.Aboard = () => PersonaCatalog.Covas;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var prompt = fixtures.Provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("\"dock-hand\": Ren — A tired dock hand, short words.", prompt, StringComparison.Ordinal);
        Assert.Contains("a \"ship\" line is read by the narrator", prompt, StringComparison.Ordinal);
        Assert.Contains("\"speaker\": string", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACastMembersBeatSaysItsLineAndTheShipSaysWhereToGoNext()
    {
        using var fixtures = new StoryFixtures(Scripted(BeatsWithSpeakers), Cast);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var chapter = fixtures.Stories.Current("F1")!.CurrentChapter!;
        var callout = new AdventureCallout(fixtures.Book);
        var said = PauseSupport.Said(callout, Now.AddMinutes(1), PauseSupport.FirstBeat(fixtures, chapter, Now.AddMinutes(1)));

        var line = Assert.Single(said, announcement => announcement.Key == $"adventure.{chapter}.0");
        var handOff = Assert.Single(said, announcement => announcement.Key == $"{AdventureCallout.HandOffPrefix}{chapter}.0");

        Assert.Equal("Anyone. Please.", line.Text);
        Assert.StartsWith("Next: dock at Maren Anchorage", handOff.Text, StringComparison.Ordinal);
    }

    private static RoundScriptedLlmProvider Scripted(string beats) => new(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(beats));
}
