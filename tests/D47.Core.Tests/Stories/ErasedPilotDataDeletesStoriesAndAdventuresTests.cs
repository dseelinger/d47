using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>An erase of pilot data deletes the stories and adventures the Commander had before it.</summary>
public sealed class ErasedPilotDataDeletesStoriesAndAdventuresTests
{
    private static readonly DateTimeOffset Erased = Now;

    private const string Later = "the-later-story";

    private static Story Picked(string id, StoryState state, DateTimeOffset at, params string[] chapters) => new()
    {
        Id = id,
        Title = id,
        PublicLayer = id,
        State = state,
        PickedAt = at,
        Chapters = chapters,
        BackstoryBefore = "My own words.",
    };

    private static Adventure Chapter(string key, string? storyId, DateTimeOffset accepted) =>
        LanternRoute(accepted) with { Key = key, StoryId = storyId, Written = accepted.AddMinutes(-3) };

    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider([]));

    /// <summary>A finished story, a running one with a chapter written after the erase, a plain adventure, and a story picked after.</summary>
    private static void Fill(StoryFixtures fixtures, string frontierId)
    {
        fixtures.Stories.Save(frontierId, Picked(Card.Id, StoryState.Finished, Erased.AddDays(-20), "old-chapter") with { StoppedAt = Erased.AddDays(-10) });
        fixtures.Stories.Save(frontierId, Picked(Other.Id, StoryState.Running, Erased.AddDays(-5), "running-chapter", "late-chapter"));
        fixtures.Stories.Save(frontierId, Picked(Later, StoryState.Finished, Erased.AddHours(1), "later-chapter") with { StoppedAt = Erased.AddHours(3) });
        fixtures.Stories.EnsureVoter(frontierId);
        fixtures.Stories.SetBeaconForgotten(frontierId, true);

        Assert.Null(fixtures.Book.Store.Save(frontierId, Chapter("plain-adventure", null, Erased.AddDays(-1))));
        Assert.Null(fixtures.Book.Store.Save(frontierId, Chapter("running-chapter", Other.Id, Erased.AddDays(-4))));
        Assert.Null(fixtures.Book.Store.Save(frontierId, Chapter("late-chapter", Other.Id, Erased.AddDays(1))));
        Assert.Null(fixtures.Book.Store.Save(frontierId, Chapter("later-chapter", Later, Erased.AddHours(2))));

        fixtures.Director.Archive.Append(new ArchivedChapter(frontierId, Card.Id, Erased.AddDays(-20), Chapter("old-chapter", Card.Id, Erased.AddDays(-19)), [], []));
        fixtures.Director.Archive.Append(new ArchivedChapter(frontierId, Later, Erased.AddHours(1), Chapter("later-archived", Later, Erased.AddHours(2)), [], []));
    }

    [Fact]
    public void EverythingPickedBeforeTheEraseIsDeleted()
    {
        using var fixtures = Fixtures();
        Fill(fixtures, "F1");

        fixtures.Director.ForgetErased("F1", Erased);

        Assert.Equal([Later], fixtures.Stories.For("F1").Select(story => story.Id));
        Assert.Equal(["later-chapter"], fixtures.Book.Store.For("F1").Select(adventure => adventure.Key));
        Assert.Equal(["later-archived"], fixtures.Director.Archive.For("F1").Select(chapter => chapter.Adventure.Key));
        Assert.Null(fixtures.Stories.Voter("F1"));
        Assert.False(fixtures.Stories.BeaconForgotten("F1"));
    }

    [Fact]
    public void AnArchivedChapterWithNoRunOnRecordGoesByItsOwnTime()
    {
        using var fixtures = Fixtures();
        fixtures.Director.Archive.Append(new ArchivedChapter("F1", Card.Id, null, Chapter("before", Card.Id, Erased.AddDays(-1)), [], []));
        fixtures.Director.Archive.Append(new ArchivedChapter("F1", Card.Id, null, Chapter("after", Card.Id, Erased.AddDays(1)), [], []));

        fixtures.Director.ForgetErased("F1", Erased);

        Assert.Equal(["after"], fixtures.Director.Archive.For("F1").Select(chapter => chapter.Adventure.Key));
    }

    [Fact]
    public void TheFilesHoldOnlyWhatIsKept()
    {
        using var fixtures = Fixtures();
        Fill(fixtures, "F1");

        fixtures.Director.ForgetErased("F1", Erased);

        var stories = StoryStore.Open(fixtures.StoryPath, fixtures.Files, NullLogger<StoryStore>.Instance);
        var adventures = new AdventureStore(fixtures.AdventuresPath, fixtures.Files, NullLogger<AdventureStore>.Instance);
        var archive = StoryChapterArchive.Open(fixtures.Files, fixtures.ArchivePath, NullLogger<StoryChapterArchive>.Instance);
        adventures.Poll();

        Assert.Equal([Later], stories.For("F1").Select(story => story.Id));
        Assert.Equal(["later-chapter"], adventures.For("F1").Select(adventure => adventure.Key));
        Assert.Equal(["later-archived"], archive.For("F1").Select(chapter => chapter.Adventure.Key));
    }

    [Fact]
    public void AnotherCommandersRecordsAreUnchanged()
    {
        using var fixtures = Fixtures();
        Fill(fixtures, "F1");
        Fill(fixtures, "F2");
        var voter = fixtures.Stories.Voter("F2");

        fixtures.Director.ForgetErased("F1", Erased);

        Assert.Equal(3, fixtures.Stories.For("F2").Count);
        Assert.Equal(4, fixtures.Book.Store.For("F2").Count);
        Assert.Equal(2, fixtures.Director.Archive.For("F2").Count);
        Assert.Equal(voter, fixtures.Stories.Voter("F2"));
        Assert.True(fixtures.Stories.BeaconForgotten("F2"));
    }

    [Fact]
    public void RunningItAgainChangesNothing()
    {
        using var fixtures = Fixtures();
        Fill(fixtures, "F1");

        fixtures.Director.ForgetErased("F1", Erased);

        var voter = fixtures.Stories.EnsureVoter("F1");
        fixtures.Stories.SetBeaconForgotten("F1", true);
        var files = new[] { fixtures.StoryPath, fixtures.AdventuresPath, fixtures.ArchivePath }.Select(fixtures.Files.ReadText).ToList();
        var changes = 0;
        fixtures.Stories.Changed += () => changes++;
        fixtures.Book.Store.Changed += () => changes++;

        fixtures.Director.ForgetErased("F1", Erased);

        Assert.Equal(0, changes);
        Assert.Equal(files, new[] { fixtures.StoryPath, fixtures.AdventuresPath, fixtures.ArchivePath }.Select(fixtures.Files.ReadText));
        Assert.Equal(voter, fixtures.Stories.Voter("F1"));
        Assert.True(fixtures.Stories.BeaconForgotten("F1"));
    }

    [Fact]
    public void ADeletedRunningStoryReleasesItsCoresAndGivesBackTheBackstory()
    {
        using var fixtures = Fixtures();
        Fill(fixtures, "F1");
        fixtures.Backstory = Other.InYourWords;
        Assert.NotEqual(HeldCores.None, fixtures.Cores("F1").Hold.Cores);

        fixtures.Director.ForgetErased("F1", Erased);

        Assert.Equal(HeldCores.None, fixtures.Cores("F1").Hold.Cores);
        Assert.Equal("My own words.", fixtures.Backstory);
    }

    [Fact]
    public void ABackstoryTheCommanderWroteSinceIsLeftAlone()
    {
        using var fixtures = Fixtures();
        Fill(fixtures, "F1");
        fixtures.Backstory = "Written after the pick.";

        fixtures.Director.ForgetErased("F1", Erased);

        Assert.Equal("Written after the pick.", fixtures.Backstory);
    }

    [Fact]
    public void ALiveEraseDeletesThemAtOnce()
    {
        using var fixtures = Fixtures();
        Fill(fixtures, "F1");
        var erases = new CommanderErases(fixtures.Files, NullLogger<CommanderErases>.Instance);
        erases.Changed += fixtures.Director.ForgetErased;

        erases.Apply(Event("{\"timestamp\":\"2026-09-30T13:30:00Z\",\"event\":\"NewCommander\",\"Name\":\"X\",\"FID\":\"F1\"}"));

        Assert.Empty(fixtures.Stories.For("F1"));
        Assert.Empty(fixtures.Book.Store.For("F1"));
        Assert.Empty(fixtures.Director.Archive.For("F1"));
    }
}
