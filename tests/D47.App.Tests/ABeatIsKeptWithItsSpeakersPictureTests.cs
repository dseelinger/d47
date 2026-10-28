using D47.App.Voice;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Messages;
using D47.Core.Persona;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A beat's line is kept on the story's feed with the picture of whoever said it.</summary>
[Trait("Category", "Integration")]
public sealed class ABeatIsKeptWithItsSpeakersPictureTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private const string Chapter = "chapter-one";

    private static readonly StorySecret Secret = StoryFixture.Secret with
    {
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
                Primary = true,
            },
        ],
    };

    private static (StoryRecords Records, D47.Core.Adventures.AdventureBook Book, string Ship) Build()
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-beat-pictures"));
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
                        new AdventureLine { Text = "Sign here." },
                    ],
                },
            ],
        }));

        var director = new StoryDirector(
            stories, surface.Book, () => catalog, surface.Generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var personas = new PersonaHost();
        var messages = new MessageStore(Path.Combine(paths.Data, "messages.json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);
        var reactions = new StoryReactionCallout(new NearbyFight()) { Window = () => director.ReactionWindow(null) };

        var records = new StoryRecords(
            messages, director, surface.Book, personas, new GameStateStore(), cast => cast is null ? null : $"{cast.Picture}", reactions);

        return (records, surface.Book, SpeakerPictures.Core(personas.Current.Id));
    }

    private static Announcement Line(int line) =>
        new(line == 0 ? $"{AdventureCallout.KeyPrefix}{Chapter}.0" : $"{AdventureCallout.LinePrefix}{Chapter}.0.{line}", "Said.");

    [Fact]
    public void ACastLineKeepsTheMembersPicture()
    {
        var (records, book, _) = Build();

        records.RecordAdventure(Line(0), null);

        Assert.Equal("the-test-story.ren", book.Store.Find(null, Chapter)!.Told[0].Picture);
    }

    [Fact]
    public void ANarratorLineKeepsTheNarratorsPicture()
    {
        var (records, book, _) = Build();

        records.RecordAdventure(Line(1), null);

        Assert.Equal(SpeakerPictures.Narrator, book.Store.Find(null, Chapter)!.Told[0].Picture);
    }

    [Fact]
    public void AShipLineKeepsTheCoresPicture()
    {
        var (records, book, ship) = Build();

        records.RecordAdventure(Line(2), null);

        Assert.Equal(ship, book.Store.Find(null, Chapter)!.Told[0].Picture);
    }
}
