using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Messages;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using D47.Core.Tests.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Messages;

[Trait("Category", "Integration")]
public class ARemovedAdventureTakesItsMessagesWithItTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-message-orphans", Guid.NewGuid().ToString("N"));

    private readonly AdventureStore _adventures;
    private readonly MessageStore _messages;
    private readonly StoryStore _stories = StoryStore.InMemory();

    public ARemovedAdventureTakesItsMessagesWithItTests()
    {
        Directory.CreateDirectory(_folder);
        _adventures = new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        _messages = new MessageStore(Path.Combine(_folder, "messages.json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private int Sweep() =>
        _messages.RemoveOrphans(key => MessageOwnership.Owned(key, _adventures, _stories, () => StoryFixtures.Catalog));

    [Fact]
    public void RemovingAnAdventureTakesItsMessagesAndLeavesTheRest()
    {
        var other = AdventureFixtures.LanternRoute() with { Key = "another-route", Name = "Another Route" };

        Assert.Null(_adventures.Save("F1", AdventureFixtures.LanternRoute()));
        Assert.Null(_adventures.Save("F1", other));

        _messages.Post("narrator", "beat", "x", Noon, "the-lantern-route");
        _messages.Post("narrator", "nudge", "x", Noon, "the-lantern-route");
        _messages.Post("narrator", "other", "x", Noon, "another-route");
        _messages.Post("narrator", "unkeyed", "x", Noon);

        Assert.Equal(0, Sweep());
        Assert.True(_adventures.Remove("F1", "the-lantern-route"));
        Assert.Equal(2, Sweep());

        Assert.Equal(["other", "unkeyed"], _messages.All.Select(message => message.Subject).Order());
    }

    [Fact]
    public void AClueForAStoryMissingFromTheCatalogGoesAndOneForACatalogStoryStays()
    {
        var storyId = StoryFixtures.Catalog.Cards[0].Id;
        _stories.Save("F1", new Story { Id = storyId, Title = "T", PublicLayer = "x", PickedAt = Noon });

        _messages.Post("narrator", "kept", "x", Noon, $"{StoryClueCallout.KeyPrefix}{storyId}.0");
        _messages.Post("narrator", "gone", "x", Noon, $"{StoryClueCallout.KeyPrefix}no-such-story.0");
        _messages.Post("narrator", "malformed", "x", Noon, StoryClueCallout.KeyPrefix);

        Assert.Equal(2, Sweep());
        Assert.Equal("kept", Assert.Single(_messages.All).Subject);
    }

    [Fact]
    public void AbandoningAnAdventureKeepsItsMessages()
    {
        var book = new AdventureBook(_adventures, NullLogger<AdventureBook>.Instance);
        book.Write("F1", AdventureFixtures.LanternRoute());
        Assert.Null(book.Begin("F1", "the-lantern-route", AdventureFixtures.Accepted));
        _messages.Post("narrator", "beat", "x", Noon, "the-lantern-route");

        Assert.Null(book.Abandon("F1", "the-lantern-route", AdventureFixtures.Accepted.AddDays(1)));

        Assert.Equal(0, Sweep());
        Assert.Single(_messages.All);
    }

    [Fact]
    public void ASweepWithNothingToRemoveDoesNotWriteOrRaiseChanged()
    {
        _messages.Post("narrator", "unkeyed", "x", Noon);

        var path = Path.Combine(_folder, "messages.json");
        var written = File.GetLastWriteTimeUtc(path);
        var raised = 0;
        _messages.Changed += () => raised++;

        Assert.Equal(0, Sweep());
        Assert.Equal(0, raised);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void ASweepThatRemovesRaisesChangedOnce()
    {
        _messages.Post("narrator", "a", "x", Noon, "gone");
        _messages.Post("narrator", "b", "x", Noon, "gone-too");

        var raised = 0;
        _messages.Changed += () => raised++;

        Assert.Equal(2, Sweep());
        Assert.Equal(1, raised);
    }
}
