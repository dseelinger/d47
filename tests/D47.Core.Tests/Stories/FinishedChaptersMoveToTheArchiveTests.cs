using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A story keeps its current chapter and the one before it in the adventure file; the rest move to the archive with
/// when their beats fired, and story chapters do not count toward the Commander's own adventures.
/// </summary>
public sealed class FinishedChaptersMoveToTheArchiveTests
{
    private static string NamedSpine(string name, string premise) =>
        $$"""
        {"name": "{{name}}", "premise": "{{premise}}", "want": "To see a beacon.",
         "stake": "Whether a tool can want.", "turn": "It asked before.", "ending": "It is answered."}
        """;

    /// <summary>
    /// The rounds for chapters one to <paramref name="chapters"/>, each named "{prefix} n" with the premise
    /// <paramref name="premise"/>, or "{prefix} premise n." without one.
    /// </summary>
    private static IEnumerable<IReadOnlyList<LlmStreamEvent>> Rounds(int chapters, string prefix = "Chapter", string? premise = null)
    {
        for (var number = 1; number <= chapters; number++)
        {
            yield return RoundScriptedLlmProvider.Saying(NamedSpine($"{prefix} {number}", premise ?? $"{prefix} premise {number}."));
            yield return RoundScriptedLlmProvider.Saying(number == 1 ? BeatsToTheBeacon : NextBeats);
        }
    }

    private static StoryFixtures Fixtures(int chapters) => new(new RoundScriptedLlmProvider([.. Rounds(chapters)]));

    /// <summary>Picks the story at <see cref="StoryFixtures.Now"/> and flies it until chapter <paramref name="chapters"/> has begun, a day a chapter.</summary>
    private static async Task FlyTo(StoryFixtures fixtures, int chapters, DateTimeOffset? from = null, string id = Id)
    {
        var start = from ?? Now;

        Assert.Null(await fixtures.Director.PickAsync("F1", id, start, CancellationToken.None));
        await Continue(fixtures, 2, chapters, start);
    }

    private static async Task Continue(StoryFixtures fixtures, int next, int last, DateTimeOffset start)
    {
        for (var number = next; number <= last; number++)
        {
            var day = start.AddDays(number - 1);

            fixtures.Finish("F1", fixtures.Stories.Current("F1")!.CurrentChapter!, day);

            var writing = fixtures.Director.Tick("F1", day.AddHours(1));
            Assert.NotNull(writing);
            Assert.Null(await writing);
            Assert.Equal(number, fixtures.Stories.Current("F1")!.Chapters.Count);
        }
    }

    private static IReadOnlyList<string> StoryChaptersOnFile(StoryFixtures fixtures) =>
        [.. fixtures.Book.Store.For("F1").Where(adventure => adventure.StoryId is not null).Select(adventure => adventure.Key)];

    [Fact]
    public async Task WhenChapterFiveBeginsTheFileHoldsFourAndFive()
    {
        using var fixtures = Fixtures(5);
        await FlyTo(fixtures, 5);

        var chapters = fixtures.Stories.Current("F1")!.Chapters;

        Assert.Equal([chapters[3], chapters[4]], StoryChaptersOnFile(fixtures));

        var reread = StoryChapterArchive.Open(fixtures.ArchivePath, NullLogger<StoryChapterArchive>.Instance).For("F1");

        Assert.Equal([chapters[0], chapters[1], chapters[2]], reread.Select(chapter => chapter.Adventure.Key));
        Assert.All(reread, chapter =>
        {
            Assert.Equal(Id, chapter.StoryId);
            Assert.Equal(Now, chapter.PickedAt);
            Assert.Equal(chapter.Adventure.Beats.Count, chapter.Fired.Count);
            Assert.Equal(chapter.Fired.Count, chapter.FiredBy.Count);
        });

        Assert.Equal(fixtures.Book.Store.Find("F1", chapters[3])!.AcceptedAt, fixtures.Book.EarliestAcceptance());
    }

    [Fact]
    public async Task TheBriefForChapterFifteenNamesTheLastTenAndCountsTheRest()
    {
        using var fixtures = Fixtures(15);
        await FlyTo(fixtures, 15);

        // Chapter n's spine is request 2(n − 1).
        var prompt = fixtures.Provider.Requests[28].Prompt.History[0].Text;

        Assert.Contains("Title: Chapter 14", prompt);

        for (var number = 4; number <= 13; number++)
        {
            Assert.Contains($"- Chapter {number}: Chapter premise {number}.", prompt);
        }

        Assert.DoesNotContain("- Chapter 3: ", prompt);
        Assert.DoesNotContain("- Chapter 1: ", prompt);
        Assert.Contains("3 chapters came before them.", prompt);
    }

    [Fact]
    public async Task StoryChaptersDoNotCountTowardTheCommandersOwnAdventures()
    {
        using var fixtures = Fixtures(2);

        for (var number = 0; number < AdventureLimits.MaxAdventures; number++)
        {
            Assert.Null(fixtures.Book.Write("F1", AdventureFixtures.LanternRoute() with { Key = $"own-{number}" }));
        }

        await FlyTo(fixtures, 2);

        Assert.Equal(2, StoryChaptersOnFile(fixtures).Count);
        Assert.NotNull(fixtures.Book.Write("F1", AdventureFixtures.LanternRoute() with { Key = "one-too-many" }));
    }

    [Fact]
    public async Task AbandoningLeavesNoChapterOnFile()
    {
        using var fixtures = Fixtures(3);
        await FlyTo(fixtures, 3);

        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(5)));

        Assert.Empty(StoryChaptersOnFile(fixtures));
        Assert.Equal(3, fixtures.Director.Archive.For("F1").Count);
    }

    [Fact]
    public async Task AStoryPickedAgainDoesNotReadTheEarlierRunsChapters()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
        [
            .. Rounds(3, "Old"),
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
            .. Rounds(4, "Old", "A new premise."),
        ]));

        await FlyTo(fixtures, 3);

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(10), CancellationToken.None));
        Assert.DoesNotContain(fixtures.Book.Store.For("F1"), adventure => adventure.StoryId == Id);

        var again = Now.AddDays(20);

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Id, again, CancellationToken.None));
        await Continue(fixtures, 2, 4, again);

        // The rerun's chapters carry the earlier run's names, and so its keys.
        var story = fixtures.Stories.Current("F1")!;
        var prompt = fixtures.Provider.Requests[^2].Prompt.History[0].Text;

        Assert.Equal(StoryChaptersOnFile(fixtures), story.Chapters.TakeLast(2));
        Assert.Contains("- Old 1: A new premise.", prompt);
        Assert.Contains("- Old 2: A new premise.", prompt);
        Assert.DoesNotContain("Old premise", prompt);
    }

    [Fact]
    public async Task ALineThatDoesNotParseIsSkipped()
    {
        using var fixtures = Fixtures(4);
        await FlyTo(fixtures, 4);

        var lines = File.ReadAllLines(fixtures.ArchivePath);
        Assert.Equal(2, lines.Length);

        File.WriteAllLines(fixtures.ArchivePath, [lines[0], "{ not json", """{"storyId":"x"}""", lines[1]]);

        var reread = StoryChapterArchive.Open(fixtures.ArchivePath, NullLogger<StoryChapterArchive>.Instance).For("F1");

        Assert.Equal(fixtures.Stories.Current("F1")!.Chapters.Take(2), reread.Select(chapter => chapter.Adventure.Key));
    }

    [Fact]
    public void ChaptersOfAnEndedStoryLeftOnFileAreMovedAtStartup()
    {
        using var fixtures = Fixtures(0);

        for (var number = 1; number <= 4; number++)
        {
            var key = $"first-light-{number}";

            Assert.Null(fixtures.Book.Write("F1", AdventureFixtures.LanternRoute(Now.AddDays(number)) with { Key = key, StoryId = Id }));
            Assert.Null(fixtures.Book.Abandon("F1", key, Now.AddDays(number).AddHours(1)));
        }

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = "A public card.",
            Chapters = ["first-light-4"],
            PickedAt = Now.AddDays(4),
            State = StoryState.Ended,
        });

        fixtures.Director.ArchiveChapters();

        Assert.Empty(fixtures.Book.Store.For("F1"));
        Assert.Empty(fixtures.Book.Standings("F1"));
        Assert.Equal(4, fixtures.Director.Archive.For("F1").Count);
        Assert.Equal(Now.AddDays(4), fixtures.Director.Archive.For("F1")[^1].PickedAt);
        Assert.Null(fixtures.Director.Archive.For("F1")[0].PickedAt);
    }
}
