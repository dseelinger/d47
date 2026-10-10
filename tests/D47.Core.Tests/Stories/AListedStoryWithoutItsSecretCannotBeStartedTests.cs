using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class AListedStoryWithoutItsSecretCannotBeStartedTests
{
    [Fact]
    public void TheCardIsListedAndTheSecretIsNull()
    {
        var folder = new DownloadedStoryFolder();
        folder.WriteIndex(StoryFixtures.Card);

        var catalog = StoryCatalog.Load(folder.Files, folder.Path);

        Assert.NotNull(catalog.Find(StoryFixtures.Card.Id));
        Assert.Null(catalog.Secret(StoryFixtures.Card.Id));
    }
}
