using D47.Core.Stories;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The Commander's own Backstory is the default; each story keeps its own, saved when it is shelved or switched off.</summary>
public sealed class TheBackstoryFollowsTheActiveStoryTests
{
    private static StoryFixtures Fixtures(int writes = 2) => new(new RoundScriptedLlmProvider(
        [.. Enumerable.Range(0, writes).SelectMany(_ => new[]
        {
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
            RoundScriptedLlmProvider.Saying(NothingObjected),
        })]));

    [Fact]
    public async Task SwitchingAToBToAKeepsEachStorysEdits()
    {
        using var fixtures = Fixtures();
        fixtures.Backstory = "Mine";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Backstory = "A edited";

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(1), CancellationToken.None));
        Assert.Equal(Other.InYourWords, fixtures.Backstory);
        fixtures.Backstory = "B edited";

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Id, Now.AddDays(2), CancellationToken.None));
        Assert.Equal("A edited", fixtures.Backstory);

        Assert.Null(await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(3), CancellationToken.None));
        Assert.Equal("B edited", fixtures.Backstory);
        Assert.Equal("Mine", fixtures.Stories.DefaultBackstory("F1"));
    }

    [Fact]
    public async Task NoStoryBringsBackTheDefaultAndPickingBringsBackTheStorysOwn()
    {
        using var fixtures = Fixtures(1);
        fixtures.Backstory = "Mine";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Backstory = "A edited";

        Assert.Null(fixtures.Director.ShelveCurrent("F1", Now.AddDays(1)));
        Assert.Equal("Mine", fixtures.Backstory);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now.AddDays(2), CancellationToken.None));
        Assert.Equal("A edited", fixtures.Backstory);
    }

    [Fact]
    public async Task SwitchingTheStoryOffBringsBackTheDefaultAndOnTheStorysOwn()
    {
        using var fixtures = Fixtures(1);
        fixtures.Backstory = "Mine";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Backstory = "A edited";

        Assert.Null(fixtures.Director.SetOn("F1", false, Now.AddHours(1)));
        Assert.Equal("Mine", fixtures.Backstory);

        Assert.Null(fixtures.Director.SetOn("F1", true, Now.AddHours(2)));
        Assert.Equal("A edited", fixtures.Backstory);

        Assert.Null(fixtures.Director.SetOn("F1", false, Now.AddHours(3)));
        Assert.Equal("Mine", fixtures.Backstory);
    }

    [Fact]
    public async Task AbandoningAfterASwitchBringsBackTheDefault()
    {
        using var fixtures = Fixtures();
        fixtures.Backstory = "Mine";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(1), CancellationToken.None));
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddDays(2)));

        Assert.Equal("Mine", fixtures.Backstory);
    }

    [Fact]
    public void AStoryFileWithABackstoryBeforeLoadsItAsTheDefault()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(MemoryInstall.FakeRoot, "story-old.json");
        var picked = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var written = StoryStore.Open(path, files, NullLogger<StoryStore>.Instance);
        written.Save("F1", new Story { Id = "a", Title = "A", PublicLayer = "A", PickedAt = picked, BackstoryBefore = "Old words" });

        var loaded = StoryStore.Open(path, files, NullLogger<StoryStore>.Instance);

        Assert.Equal("Old words", loaded.DefaultBackstory("F1"));
        Assert.Null(loaded.Find("F1", "a")!.BackstoryBefore);
    }

    [Fact]
    public async Task ErasingPilotDataRemovesTheDefaultAndTheSavedBackstories()
    {
        using var fixtures = Fixtures(1);
        fixtures.Backstory = "Mine";

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Null(fixtures.Director.ShelveCurrent("F1", Now.AddDays(1)));

        fixtures.Director.ForgetErased("F1", Now.AddDays(2));

        Assert.Empty(fixtures.Stories.For("F1"));
        Assert.Null(fixtures.Stories.DefaultBackstory("F1"));
    }
}
