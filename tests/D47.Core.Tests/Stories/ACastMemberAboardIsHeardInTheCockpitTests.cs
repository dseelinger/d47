using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A cast member aboard the Commander's ship is heard in the cockpit, without their comms link: between the beats their
/// span names, or for the one chapter that brings them aboard (#1600).
/// </summary>
public sealed class ACastMemberAboardIsHeardInTheCockpitTests
{
    private static readonly StorySpeaker Rider = new()
    {
        Id = "rider",
        Name = "Vale",
        Who = "A courier riding along, dry and patient.",
        Provider = StorySpeaker.Kokoro,
        Voice = "bm_lewis",
        Link = 0.3,
        Aboard = new("midpoint", "allIsLost"),
    };

    private static readonly StorySecret Riding = Secret with { Cast = [.. Secret.Cast, Rider] };

    [Theory]
    [InlineData(StoryStage.ActOne, false)]
    [InlineData(StoryStage.FunAndGames, false)]
    [InlineData(StoryStage.Midpoint, true)]
    [InlineData(StoryStage.BadGuysCloseIn, true)]
    [InlineData(StoryStage.AllIsLost, false)]
    [InlineData(StoryStage.Finale, false)]
    public void AMemberIsAboardBetweenTheirBeatsAndNotOutsideThem(StoryStage stage, bool aboard) =>
        Assert.Equal(aboard, StoryAboard.Of(Riding, StoryPacing.OneYear, stage, beaconInReach: true, finaleChapter: 1).Contains("rider"));

    [Fact]
    public void ASpanWithoutBeatsRunsTheWholeStoryAndAMemberWithoutOneIsNeverAboard()
    {
        var always = Secret with { Cast = [.. Secret.Cast, Rider with { Aboard = new(null, null) }] };

        Assert.All(Enum.GetValues<StoryStage>(), stage =>
            Assert.Equal(["rider"], StoryAboard.Of(always, StoryPacing.OneYear, stage, beaconInReach: false, finaleChapter: 4)));
    }

    [Fact]
    public void AShortStoryBoardsAtTheStageThatHandsOutItsBeat()
    {
        var week = Secret with { Cast = [Rider with { Aboard = new("breakIntoTwo", "allIsLost") }] };

        Assert.Empty(StoryAboard.Of(week, StoryPacing.OneWeek, StoryStage.ActOne, beaconInReach: false, finaleChapter: null));
        Assert.Equal(["rider"], StoryAboard.Of(week, StoryPacing.OneWeek, StoryStage.ActOne, beaconInReach: true, finaleChapter: null));
        Assert.Equal(["rider"], StoryAboard.Of(week, StoryPacing.OneWeek, StoryStage.Midpoint, beaconInReach: true, finaleChapter: null));
        Assert.Empty(StoryAboard.Of(week, StoryPacing.OneWeek, StoryStage.AllIsLost, beaconInReach: true, finaleChapter: null));
    }

    [Fact]
    public void AnAboardMembersLineCarriesNoLinkTreatment()
    {
        var radio = StoryVoices.Of("rider", Riding, null, PersonaCatalog.Kex);
        var cockpit = StoryVoices.Of("rider", Riding, null, PersonaCatalog.Kex, aboard: ["rider"]);

        Assert.Equal(0.3, radio.Pinned!.Link);
        Assert.NotNull(CastVoice.Treatment(radio.Pinned));
        Assert.Null(cockpit.Pinned!.Link);
        Assert.Null(CastVoice.Treatment(cockpit.Pinned));
    }

    [Fact]
    public void AnAboardMembersEffectsAndChosenVoiceKeepAllButTheLink()
    {
        var glitching = Secret with { Cast = [Rider with { Effects = [new("glitch", 12)] }] };
        var choices = new Dictionary<string, StoryVoiceChoice> { [$"{Id}.rider"] = new(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb") };

        var cockpit = StoryVoices.Of("rider", glitching, null, PersonaCatalog.Kex, choices, aboard: ["rider"]).Pinned!;

        Assert.Equal("JBFqnCBsd6RMkjVDRZzb", cockpit.VoiceId);
        Assert.Null(cockpit.Link);
        Assert.Null(cockpit.Fallback!.Link);
        Assert.Equal([new StorySpeakerEffect("glitch", 12)], cockpit.Effects);
        Assert.NotNull(CastVoice.Treatment(cockpit));
    }

    [Fact]
    public void ASealedEntryWithoutTheFieldLoadsAsBefore()
    {
        var loaded = Assert.Single(StoryCatalog.Unseal(Sealed.Of(Secret)));

        Assert.DoesNotContain("aboard", Sealed.Json(Secret), StringComparison.Ordinal);
        Assert.All(loaded.Cast, member => Assert.Null(member.Aboard));
        Assert.Empty(StoryCatalog.Faults(Card, loaded));
    }

    [Fact]
    public void ASealedEntryWithASpanReadsItBack() =>
        Assert.Equal(new StorySpeakerAboard("midpoint", "allIsLost"), Assert.Single(StoryCatalog.Unseal(Sealed.Of(Riding))).Cast[^1].Aboard);

    [Fact]
    public async Task TheChapterWriterIsToldWhoIsAboardAndTheChapterRecordsThem()
    {
        var always = Secret with { Cast = [.. Secret.Cast, Rider with { Aboard = new(null, null) }] };
        using var fixtures = new StoryFixtures(Scripted(BeatsToTheBeacon), always);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var key = fixtures.Stories.Current("F1")!.CurrentChapter!;
        Assert.Equal(["rider"], fixtures.Book.Store.Find("F1", key)!.Aboard);
        Assert.Contains(fixtures.Provider.Requests, request => request.Prompt.History[0].Text.Contains(
            "Aboard the Commander's ship for this chapter: Vale.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AChaptersAboardHoldsForThatChapterOnly()
    {
        var unspanned = Secret with { Cast = [.. Secret.Cast, Rider with { Aboard = null }] };
        var boarding = BeatsToTheBeacon.Replace(
            "\"reply\": \"Here it is.\",", "\"reply\": \"Here it is.\", \"aboard\": [\"RIDER\", \"stranger\", \"ship\"],", StringComparison.Ordinal);
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine),
                RoundScriptedLlmProvider.Saying(boarding),
                RoundScriptedLlmProvider.Saying(NothingObjected),
                RoundScriptedLlmProvider.Saying(NextSpine),
                RoundScriptedLlmProvider.Saying(NextBeats),
                RoundScriptedLlmProvider.Saying(NothingObjected)),
            unspanned);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var first = fixtures.Stories.Current("F1")!.CurrentChapter!;
        Assert.Equal(["rider"], fixtures.Director.CastAboard("F1"));
        Assert.Null(fixtures.Director.LineVoice("F1", "rider")!.Pinned!.Link);

        var reopened = new AdventureStore(fixtures.AdventuresPath, fixtures.Files, NullLogger<AdventureStore>.Instance);
        Assert.True(reopened.Poll());
        Assert.Equal(["rider"], reopened.Find("F1", first)!.Aboard);

        fixtures.Finish("F1", first, Now);
        Assert.Null(await fixtures.Director.Tick("F1", Now.AddDays(3))!);

        Assert.Equal(2, fixtures.Stories.Current("F1")!.Chapters.Count);
        Assert.Empty(fixtures.Director.CastAboard("F1"));
        Assert.Equal(0.3, fixtures.Director.LineVoice("F1", "rider")!.Pinned!.Link);
    }

    private static RoundScriptedLlmProvider Scripted(string beats) => new(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(beats),
        RoundScriptedLlmProvider.Saying(NothingObjected));

    /// <summary>A hidden entry sealed as the story release seals it, with unset fields left out.</summary>
    private static class Sealed
    {
        private static readonly System.Text.Json.JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        public static string Json(StorySecret secret) => System.Text.Json.JsonSerializer.Serialize(new[] { secret }, Options);

        public static string Of(StorySecret secret)
        {
            using var packed = new MemoryStream();

            using (var deflate = new System.IO.Compression.DeflateStream(packed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(System.Text.Encoding.UTF8.GetBytes(Json(secret)));
            }

            return Convert.ToBase64String(packed.ToArray());
        }
    }
}
