using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public class APickedAgainStoryKeepsItsEarlierRunTests
{
    private static readonly DateTimeOffset First = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Second = First.AddDays(10);

    private static Story Run(DateTimeOffset pickedAt, StoryState state = StoryState.Running, params string[] chapters) => new()
    {
        Id = StoryFixtures.Id,
        Title = "The Test Story",
        PublicLayer = "x",
        PickedAt = pickedAt,
        State = state,
        Chapters = chapters,
    };

    [Fact]
    public void ASecondPickIsAddedBesideTheFirst()
    {
        var store = InMemoryStories.Create();
        store.Save("F1", Run(First, StoryState.Finished, "a"));
        store.Save("F1", Run(Second));

        Assert.Equal(2, store.For("F1").Count);
        Assert.Equal(StoryState.Finished, store.For("F1")[0].State);
        Assert.Equal(["a"], store.For("F1")[0].Chapters);
    }

    [Fact]
    public void ASaveWithTheSamePickedAtReplacesItsOwnRun()
    {
        var store = InMemoryStories.Create();
        store.Save("F1", Run(First));
        store.Save("F1", Run(First) with { State = StoryState.Paused });

        Assert.Equal(StoryState.Paused, Assert.Single(store.For("F1")).State);
    }

    [Fact]
    public void FindAndUpdateTakeTheNewestRun()
    {
        var store = InMemoryStories.Create();
        store.Save("F1", Run(Second));
        store.Save("F1", Run(First, StoryState.Finished));

        Assert.Equal(Second, store.Find("F1", StoryFixtures.Id)!.PickedAt);

        store.Update("F1", StoryFixtures.Id, story => story with { Title = "Changed" });

        Assert.Equal("Changed", store.For("F1").Single(story => story.PickedAt == Second).Title);
        Assert.Equal("The Test Story", store.For("F1").Single(story => story.PickedAt == First).Title);
    }

    [Fact]
    public void ErasingRemovesEveryRunPickedAtOrBeforeTheTime()
    {
        var store = InMemoryStories.Create();
        store.Save("F1", Run(First, StoryState.Finished));
        store.Save("F1", Run(Second));

        var removed = store.RemovePickedBy("F1", First.AddDays(5));

        Assert.Equal(First, Assert.Single(removed).PickedAt);
        Assert.Equal(Second, Assert.Single(store.For("F1")).PickedAt);
    }
}
