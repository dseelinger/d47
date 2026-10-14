using D47.App.Voice;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Messages;
using D47.Core.Persona;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A clue spoken by a cast member not yet introduced, or a line of a chapter stored without introductions, is preceded by
/// the narrator's introduction; once that is said it is posted from the narrator and the member is marked introduced.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AStrangerIsIntroducedBeforeTheyFirstSpeakTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private const string Introduction = "Ren is the dock hand who signs the Commander's berth.";

    private const string Chapter = "chapter-one";

    private static readonly StorySecret Secret = StoryFixture.Secret with
    {
        Clues = [new("Bay four was never empty.", "ren"), .. StoryFixture.Secret.Clues],
        Cast =
        [
            new StorySpeaker
            {
                Id = "ren",
                Name = "Ren",
                Who = "A tired dock hand, short words.",
                Role = "the dock hand who signs the Commander's berth",
                Provider = StorySpeaker.Kokoro,
                Voice = "bm_george",
            },
        ],
    };

    private sealed record Built(LineWriter Writer, StoryRecords Records, StoryDirector Director, MessageStore Messages);

    private static Built Build(params string[] replies)
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-introductions"));
        var surface = AdventureFixture.Surface(paths);
        var files = TestSurface.FilesFor(paths);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), files, NullLogger<StoryStore>.Instance);
        var catalog = new StoryCatalog([StoryFixture.Story], () => [Secret]);

        stories.Save(null, new Story
        {
            Id = StoryFixture.Story.Id,
            Title = StoryFixture.Story.Title,
            PublicLayer = StoryFixture.Story.Describe(),
            Chapters = [Chapter],
            PickedAt = Now.AddHours(-1),
            Sessions = 1,
        });

        Assert.Null(surface.Book.Store.Save(null, new Adventure
        {
            Key = Chapter,
            Name = "Yard Paint",
            StoryId = StoryFixture.Story.Id,
            Beats =
            [
                new AdventureBeat
                {
                    Title = "Bay Four",
                    Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1, System = "Ossen's Lantern" },
                    Lines =
                    [
                        new AdventureLine { Text = "Bay four.", Speaker = "ren" },
                        new AdventureLine { Text = "The yard lights stay on.", Speaker = StorySpeaker.Narrator },
                        new AdventureLine { Text = "Sign here.", Speaker = "ren" },
                    ],
                },
            ],
        }));

        var director = new StoryDirector(
            stories, surface.Book, () => catalog, surface.Generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var (settings, _, _, registry, _) = TestSurface.CreateFull();
        var spend = new SpendTracker();
        var turns = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            spend,
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            replies.Length > 0 ? new Scripted(replies) : null);

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
            _ => throw new InvalidOperationException("No cast is looked up here."),
            () => false,
            PictureOf,
            reactions);

        return new Built(writer, new StoryRecords(messages, director, surface.Book, personas, gameState, PictureOf, reactions), director, messages);
    }

    private static Announcement Clue() =>
        new(StoryClueCallout.Key(new StoryClueDue(StoryFixture.Story.Id, 0)), string.Empty) { Voice = VoiceRole.Crew };

    [Fact]
    public async Task AClueIsPrecededByTheIntroductionInTheNarratorsVoice()
    {
        var built = Build("Bay four was never empty, you know.");

        var said = await built.Writer.WriteAsync(Clue());

        Assert.Equal(2, said.Count);
        Assert.Equal(StoryIntroductions.Key(StoryFixture.Story.Id, "ren"), said[0].Key);
        Assert.Equal(Introduction, said[0].Text);
        Assert.Equal(VoiceRole.Narrator, said[0].Voice);
        Assert.Null(said[0].Cast);
        Assert.Equal("ren", said[1].Cast);
        Assert.Equal("Bay four was never empty, you know.", said[1].Text);
    }

    [Fact]
    public async Task OnceSaidTheIntroductionIsPostedFromTheNarratorAndNotOwedAgain()
    {
        var built = Build("Bay four was never empty, you know.");
        var said = await built.Writer.WriteAsync(Clue());

        built.Records.RecordIntroduction(said[0], null);

        var posted = Assert.Single(built.Messages.All);

        Assert.Equal(MessageStore.Narrator, posted.From);
        Assert.Equal(Introduction, posted.Body);
        Assert.Equal(StoryFixture.Story.Title, posted.Subject);
        Assert.Null(built.Director.Introduction(null, "ren"));
        Assert.Empty(built.Writer.Introducing(said[1]));
    }

    [Fact]
    public async Task AChapterStoredWithoutIntroductionsIntroducesTheMemberAtTheirFirstLineOnly()
    {
        var built = Build();

        var first = await built.Writer.WriteAsync(new Announcement($"{AdventureCallout.KeyPrefix}{Chapter}.0", "Bay four."));
        var later = await built.Writer.WriteAsync(new Announcement($"{AdventureCallout.LinePrefix}{Chapter}.0.2", "Sign here."));

        Assert.Equal([Introduction, "Bay four."], first.Select(line => line.Text));
        Assert.Equal(VoiceRole.Narrator, first[0].Voice);
        Assert.Equal("Sign here.", Assert.Single(later).Text);
    }

    /// <summary>A model that says the scripted replies in order.</summary>
    private sealed class Scripted(params string[] replies) : ILlmProvider
    {
        private int _asked;

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
            await Task.Yield();

            yield return new LlmStreamEvent.TextDelta(replies[_asked++]);
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
        }
    }
}
