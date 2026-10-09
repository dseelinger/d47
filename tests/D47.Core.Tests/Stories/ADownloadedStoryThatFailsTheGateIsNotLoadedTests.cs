using D47.Core.Stories;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Stories;

[Trait("Category", "Integration")]
public sealed class ADownloadedStoryThatFailsTheGateIsNotLoadedTests
{
    [Fact]
    public void ThirteenCluesForAYearAreNotLoadedAndTheLogNamesTheFieldOnly()
    {
        using var folder = new DownloadedStoryFolder();
        folder.WriteIndex(StoryFixtures.Card);
        folder.WriteSealed(StoryFixtures.Secret with { Clues = [.. StoryFixtures.Secret.Clues.Skip(1)] });
        var log = new RecordingLogger();

        var catalog = StoryCatalog.Load(folder.Path, log);

        Assert.NotNull(catalog.Find(StoryFixtures.Card.Id));
        Assert.Null(catalog.Secret(StoryFixtures.Card.Id));

        var warning = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning).Message;
        Assert.Contains(StoryFixtures.Card.Id, warning);
        Assert.Contains("clues", warning);
        Assert.DoesNotContain(StoryFixtures.Secret.Clues[1].Text, warning);
    }
}
