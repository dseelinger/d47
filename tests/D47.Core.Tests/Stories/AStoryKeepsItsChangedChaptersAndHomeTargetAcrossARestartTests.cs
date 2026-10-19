using D47.Core.Stories;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

[Trait("Category", "Integration")]
public sealed class AStoryKeepsItsChangedChaptersAndHomeTargetAcrossARestartTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-" + Guid.NewGuid().ToString("N"));

    public AStoryKeepsItsChangedChaptersAndHomeTargetAcrossARestartTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void TheChangedChaptersAndTheTargetSurviveReopeningTheFile()
    {
        var path = Path.Combine(_folder, "story.json");
        var card = StoryFixtures.Card;

        StoryStore.Open(path, new DiskFileSystem(), NullLogger<StoryStore>.Instance).Save("F1", new Story
        {
            Id = card.Id,
            Title = card.Title,
            PublicLayer = card.Describe(),
            Chapters = ["one", "two"],
            ChangedChapters = ["one", "two"],
            HomeTarget = "Colonia",
        });

        var reopened = StoryStore.Open(path, new DiskFileSystem(), NullLogger<StoryStore>.Instance).Current("F1")!;

        Assert.Equal(["one", "two"], reopened.ChangedChapters);
        Assert.Equal("Colonia", reopened.HomeTarget);
    }
}
