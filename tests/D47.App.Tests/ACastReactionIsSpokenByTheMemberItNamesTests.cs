using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core;
using D47.Core.Journal;
using D47.Core.Messages;
using D47.Core.Persona;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A cast reaction is written by the model as the primary member it names, spoken in that member's voice and posted to
/// the Story log from them with their picture. The prompt carries the event as the classifier recorded it, the hidden
/// layer and no live game state; a reply naming anyone outside the primary cast is not spoken.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ACastReactionIsSpokenByTheMemberItNamesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static readonly StorySecret Secret = StoryFixture.Secret with
    {
        Cast =
        [
            new StorySpeaker { Id = "ren", Name = "Ren", Who = "A tired dock hand, short words.", Provider = StorySpeaker.Kokoro, Voice = "bm_george", Primary = true },
            new StorySpeaker { Id = "ama", Name = "Ama", Who = "A station clerk.", Provider = StorySpeaker.Kokoro, Voice = "af_heart" },
            new StorySpeaker { Id = "yui", Name = "Yui", Who = "A courier pilot.", Provider = StorySpeaker.Kokoro, Voice = "af_heart", Primary = true, Language = "ja" },
        ],
    };

    private sealed record Built(
        LineWriter Writer, StoryRecords Records, StoryReactionCallout Reactions, StoryDirector Director, MessageStore Messages, Scripted Model);

    private static Built Build(params string[] replies)
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-cast-reaction"));
        var surface = AdventureFixture.Surface(paths);
        var files = TestSurface.FilesFor(paths);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), files, NullLogger<StoryStore>.Instance);
        var catalog = new StoryCatalog([StoryFixture.Story], () => [Secret]);

        stories.Save(null, new Story
        {
            Id = StoryFixture.Story.Id,
            Title = StoryFixture.Story.Title,
            PublicLayer = StoryFixture.Story.Describe(),
            Chapters = ["chapter-one"],
            PickedAt = Now.AddHours(-1),
            Sessions = 1,
        });

        var director = new StoryDirector(
            stories, surface.Book, () => catalog, surface.Generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var (settings, _, _, registry, _) = TestSurface.CreateFull();
        var spend = new SpendTracker();
        var model = new Scripted(replies);
        var turns = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            spend,
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            replies.Length > 0 ? model : null)
        {
            LiveGameState = () => "LIVE GAME STATE",
        };

        var personas = new PersonaHost();
        var gameState = new GameStateStore();
        var messages = new MessageStore(Path.Combine(paths.Data, "messages.json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);
        var reactions = new StoryReactionCallout(new NearbyFight()) { Window = () => director.ReactionWindow(null) };

        static string? PictureOf(StorySpeakerShown? cast) => cast is null ? null : $"pictures/{cast.Picture}.jpg";

        var writer = new LineWriter(
            settings,
            personas,
            gameState,
            turns,
            spend,
            NullLogger.Instance,
            director,
            surface.Book,
            () => null,
            new NearbyFight(),
            new SceneTracker(),
            () => GameStatus.Unknown,
            _ => false,
            _ => null,
            _ => throw new InvalidOperationException("No cast is looked up for a reaction."),
            () => false,
            PictureOf,
            reactions);

        var records = new StoryRecords(messages, director, surface.Book, personas, gameState, PictureOf, reactions);

        return new Built(writer, records, reactions, director, messages, model);
    }

    /// <summary>The marker the callout queues once the Commander, interdicted in space, docks.</summary>
    private static Announcement Marker(Built built)
    {
        var flying = GameStatus.Unknown with { Flags = StatusFlags.InMainShip, ReadAt = Now };
        var docked = GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip, ReadAt = Now };

        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-10-13T12:00:00Z", "event":"Interdicted", "Submitted":false, "Interdictor":"Dedy Sofyan", "IsPlayer":false, "Faction":"The Brothers" }""",
            NullLogger.Instance,
            out var interdicted));

        Assert.Empty(built.Reactions.Examine(new CalloutContext(Now, false, null, flying, NavRoute.None, [interdicted!])));

        return Assert.Single(built.Reactions.Examine(new CalloutContext(Now.AddMinutes(10), false, null, docked, NavRoute.None, [])));
    }

    [Fact]
    public async Task TheLineIsSpokenInTheMembersVoiceAndPostedFromThemWithThePicture()
    {
        var built = Build("ren: Heard they dragged you out of the black and you fought it.");
        var marker = Marker(built);

        var said = Assert.Single(await built.Writer.WriteAsync(marker));

        Assert.Equal("Heard they dragged you out of the black and you fought it.", said.Text);
        Assert.Equal(VoiceRole.Crew, said.Voice);
        Assert.Equal("Ren", said.Speaker);
        Assert.Equal(new PinnedVoice(StorySpeaker.Kokoro, "bm_george"), said.Pinned);
        Assert.Equal("pictures/the-test-story.ren.jpg", said.Picture);
        Assert.Equal((new StoryReactionDue(StoryFixture.Story.Id, 1), "ren"), StoryReactionCallout.Parse(said.Key));

        built.Records.RecordReaction(said, null);

        var posted = Assert.Single(built.Messages.All);

        Assert.Equal("Ren", posted.From);
        Assert.Equal(StoryFixture.Story.Title, posted.Subject);
        Assert.Equal(said.Text, posted.Body);
        Assert.Equal("pictures/the-test-story.ren.jpg", posted.Picture);
        Assert.Equal("the-test-story.ren", posted.Cast);
        Assert.Equal(said.Key, posted.AdventureKey);

        Assert.True(built.Director.ReactionWindow(null)!.Reacted);
        Assert.Null(built.Reactions.Newest(null, new StoryReactionDue(StoryFixture.Story.Id, 1)));
    }

    [Fact]
    public async Task ThePromptCarriesTheRecordedEventAndTheHiddenLayerButNoNameOrLiveState()
    {
        var built = Build("ren: Fought it, did you.");

        await built.Writer.WriteAsync(Marker(built));

        var prompt = Assert.Single(built.Model.Requests).Prompt;
        var instruction = Assert.Single(prompt.History).Text;

        Assert.Contains("Pulled out of supercruise by a ship of The Brothers; they fought it.", instruction);
        Assert.Contains("- ren: Ren. A tired dock hand, short words.", instruction);
        Assert.DoesNotContain("Ama", instruction);
        Assert.DoesNotContain("Dedy Sofyan", instruction);
        Assert.Null(prompt.LiveGameState);
        Assert.Contains(Secret.Secret, prompt.HiddenStory);
    }

    [Fact]
    public async Task AJapaneseMemberIsAskedForTheJapaneseAndItsEnglishAndSpeaksTheJapanese()
    {
        var built = Build("yui: 無事でよかった。\n///\nGood to see you made it.");

        var said = Assert.Single(await built.Writer.WriteAsync(Marker(built)));

        Assert.Equal("無事でよかった。", said.Text);
        Assert.Equal("Good to see you made it.", said.Translation);
        Assert.Equal("ja", said.Language);
        Assert.Equal("Yui", said.Speaker);

        var instruction = Assert.Single(Assert.Single(built.Model.Requests).Prompt.History).Text;

        Assert.Contains("- yui: Yui. A courier pilot. Speaks Japanese.", instruction);
        Assert.Contains("///", instruction);
    }

    [Fact]
    public async Task AJapaneseReplyWithNoEnglishIsNotSpoken()
    {
        var built = Build("yui: 無事でよかった。");

        Assert.Empty(await built.Writer.WriteAsync(Marker(built)));
        Assert.False(built.Director.ReactionWindow(null)!.Reacted);
    }

    [Fact]
    public async Task AnEnglishMemberAmongJapaneseOnesCarriesNoTranslation()
    {
        var built = Build("ren: Fought it, did you.");

        var said = Assert.Single(await built.Writer.WriteAsync(Marker(built)));

        Assert.Null(said.Translation);
        Assert.Null(said.Language);
    }

    [Theory]
    [InlineData("ama: I logged it.")]
    [InlineData("narrator: The black is quiet.")]
    [InlineData("They pulled you out, then.")]
    public async Task AReplyNamingAnyoneOutsideThePrimaryCastIsNotSpoken(string reply)
    {
        var built = Build(reply);

        Assert.Empty(await built.Writer.WriteAsync(Marker(built)));
        Assert.False(built.Director.ReactionWindow(null)!.Reacted);
    }

    [Fact]
    public async Task WithoutAModelNothingIsSpokenAndTheSessionIsNotUsed()
    {
        var built = Build();

        Assert.Empty(await built.Writer.WriteAsync(Marker(built)));
        Assert.False(built.Director.ReactionWindow(null)!.Reacted);
        Assert.NotNull(built.Reactions.Newest(null, new StoryReactionDue(StoryFixture.Story.Id, 1)));
    }

    /// <summary>A model that says the scripted replies in order and keeps every request.</summary>
    private sealed class Scripted(params string[] replies) : ILlmProvider
    {
        public List<LlmRequest> Requests { get; } = [];

        public string Id => "anthropic";

        public string DisplayName => "Scripted";

        public string DefaultModel => "claude-sonnet-5-5";

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = false,
            SupportsThinkingEffort = false,
            SupportsOperatorSystemMessages = false,
            MinimumCacheablePrefixTokens = 0,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await Task.Yield();

            yield return new LlmStreamEvent.TextDelta(replies[Requests.Count - 1]);
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
        }
    }
}
