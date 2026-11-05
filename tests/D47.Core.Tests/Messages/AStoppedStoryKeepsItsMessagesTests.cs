using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Messages;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Messages;

[Trait("Category", "Integration")]
public class AStoppedStoryKeepsItsMessagesTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly MessageOrphanRig _rig = new("d47-story-message-orphans");

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _rig.Dispose();
    }

    private int Sweep() => _rig.Sweep();

    private static string Id => StoryFixtures.Id;

    private void Pick(StoryState state, params string[] chapters) =>
        _rig.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = "The Test Story",
            PublicLayer = "x",
            Chapters = chapters,
            State = state,
            PickedAt = Noon,
        });

    private void PostEverything(string chapter)
    {
        _rig.Messages.Post("narrator", "beat", "x", Noon, chapter);
        _rig.Messages.Post("narrator", "clue", "x", Noon, $"{StoryClueCallout.KeyPrefix}{Id}.0");
        _rig.Messages.Post("narrator", "end", "x", Noon, StoryEnding.Key(Id));
        _rig.Messages.Post("narrator", "scan", "x", Noon, StoryLines.Key(Id));
        _rig.Messages.Post("Ren", "reaction", "x", Noon, StoryReactionCallout.Key(new StoryReactionDue(Id, 1), "dock-hand"));
    }

    private void SaveChapter(string key) =>
        Assert.Null(_rig.Adventures.Save("F1", AdventureFixtures.LanternRoute() with { Key = key, StoryId = Id }));

    [Theory]
    [InlineData(StoryState.Ended)]
    [InlineData(StoryState.Abandoned)]
    public void AnAbandonedOrSwitchedStoryKeepsItsMessages(StoryState state)
    {
        SaveChapter("chapter-1");
        _rig.Adventures.Save("F1", AdventureFixtures.LanternRoute());
        Pick(StoryState.Running, "chapter-1");
        PostEverything("chapter-1");
        _rig.Messages.Post("narrator", "own", "x", Noon, "the-lantern-route");
        _rig.Messages.Post("narrator", "unkeyed", "x", Noon);

        Assert.Equal(0, Sweep());

        Pick(state, "chapter-1");

        Assert.Equal(0, Sweep());
        Assert.Equal(7, _rig.Messages.All.Count);
    }

    [Fact]
    public void AFinishedStoryKeepsItsMessages()
    {
        SaveChapter("chapter-1");
        Pick(StoryState.Finished, "chapter-1");
        PostEverything("chapter-1");

        Assert.Equal(0, Sweep());
        Assert.Equal(5, _rig.Messages.All.Count);
    }

    [Fact]
    public void ARunningStoryKeepsAListedChapterThatIsNoLongerOnFile()
    {
        Pick(StoryState.Running, "chapter-4");
        _rig.Messages.Post("narrator", "beat", "x", Noon, "chapter-4");

        Assert.Equal(0, Sweep());
    }

    [Fact]
    public void ARunningStoryLosesTheMessagesOfAChapterItNoLongerListsOnceAnotherPickReplacesIt()
    {
        SaveChapter("chapter-1");
        SaveChapter("chapter-2");
        Pick(StoryState.Running, "chapter-2");
        _rig.Messages.Post("narrator", "old", "x", Noon, "chapter-1");
        _rig.Messages.Post("narrator", "new", "x", Noon, "chapter-2");

        Assert.Equal(1, Sweep());
        Assert.Equal("new", Assert.Single(_rig.Messages.All).Subject);
    }

    [Fact]
    public void AStoryOutsideTheCatalogLosesItsMessages()
    {
        _rig.Stories.Save("F1", new Story { Id = "retired", Title = "Retired", PublicLayer = "x", Chapters = ["gone"], PickedAt = Noon });
        _rig.Messages.Post("narrator", "clue", "x", Noon, $"{StoryClueCallout.KeyPrefix}retired.0");
        _rig.Messages.Post("narrator", "beat", "x", Noon, "gone");

        Assert.Equal(2, Sweep());
        Assert.Empty(_rig.Messages.All);
    }

    [Fact]
    public void FourAbandonedChaptersOfAnEndedStoryKeepOnlyTheOneItStillLists()
    {
        string[] chapters = ["my-own-mayday", "my-own-mayday-2", "my-own-mayday-3", "my-own-mayday-4"];

        foreach (var chapter in chapters)
        {
            SaveChapter(chapter);
            _rig.Messages.Post("covas", "beat", "x", Noon, chapter);
        }

        Pick(StoryState.Ended, "my-own-mayday-4");

        Assert.Equal(3, Sweep());
        Assert.Equal("my-own-mayday-4", Assert.Single(_rig.Messages.All).AdventureKey);
    }
}
