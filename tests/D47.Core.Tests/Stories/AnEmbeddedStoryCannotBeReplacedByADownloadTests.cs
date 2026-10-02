using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

public sealed class AnEmbeddedStoryCannotBeReplacedByADownloadTests
{
    [Fact]
    public void TheEmbeddedCardAndSecretWinAndANewIdFollows()
    {
        var embedded = new StoryCatalog([StoryFixtures.Card], () => [StoryFixtures.Secret]);
        var other = StoryFixtures.Card with { Id = "downloaded-only", Title = "Downloaded" };

        using var folder = new DownloadedStoryFolder();
        folder.WriteIndex(StoryFixtures.Card with { Title = "Replacement" }, other);
        folder.WriteSealed(StoryFixtures.Secret with { Secret = "replacement secret" });
        folder.WriteSealed(StoryFixtures.Secret with { Id = other.Id });

        var combined = StoryCatalog.Combine(embedded, StoryCatalog.Load(folder.Path));

        Assert.Equal([StoryFixtures.Card.Id, other.Id], combined.Cards.Select(card => card.Id));
        Assert.Equal(StoryFixtures.Card.Title, combined.Find(StoryFixtures.Card.Id)?.Title);
        Assert.Equal(StoryFixtures.Secret.Secret, combined.Secret(StoryFixtures.Card.Id)?.Secret);
        Assert.NotNull(combined.Secret(other.Id));
    }
}
