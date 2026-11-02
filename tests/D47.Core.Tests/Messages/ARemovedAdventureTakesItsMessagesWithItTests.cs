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

    private readonly MessageOrphanRig _rig = new("d47-message-orphans");

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _rig.Dispose();
    }

    private int Sweep() => _rig.Sweep();

    [Fact]
    public void RemovingAnAdventureTakesItsMessagesAndLeavesTheRest()
    {
        var other = AdventureFixtures.LanternRoute() with { Key = "another-route", Name = "Another Route" };

        Assert.Null(_rig.Adventures.Save("F1", AdventureFixtures.LanternRoute()));
        Assert.Null(_rig.Adventures.Save("F1", other));

        _rig.Messages.Post("narrator", "beat", "x", Noon, "the-lantern-route");
        _rig.Messages.Post("narrator", "nudge", "x", Noon, "the-lantern-route");
        _rig.Messages.Post("narrator", "other", "x", Noon, "another-route");
        _rig.Messages.Post("narrator", "unkeyed", "x", Noon);

        Assert.Equal(0, Sweep());
        Assert.True(_rig.Adventures.Remove("F1", "the-lantern-route"));
        Assert.Equal(2, Sweep());

        Assert.Equal(["other", "unkeyed"], _rig.Messages.All.Select(message => message.Subject).Order());
    }

    [Fact]
    public void AClueForAStoryMissingFromTheCatalogGoesAndOneForACatalogStoryStays()
    {
        var storyId = StoryFixtures.Catalog.Cards[0].Id;
        _rig.Stories.Save("F1", new Story { Id = storyId, Title = "T", PublicLayer = "x", PickedAt = Noon });

        _rig.Messages.Post("narrator", "kept", "x", Noon, $"{StoryClueCallout.KeyPrefix}{storyId}.0");
        _rig.Messages.Post("narrator", "gone", "x", Noon, $"{StoryClueCallout.KeyPrefix}no-such-story.0");
        _rig.Messages.Post("narrator", "malformed", "x", Noon, StoryClueCallout.KeyPrefix);

        Assert.Equal(2, Sweep());
        Assert.Equal("kept", Assert.Single(_rig.Messages.All).Subject);
    }

    [Fact]
    public void AbandoningAnAdventureKeepsItsMessages()
    {
        var book = new AdventureBook(_rig.Adventures, NullLogger<AdventureBook>.Instance);
        book.Write("F1", AdventureFixtures.LanternRoute());
        Assert.Null(book.Begin("F1", "the-lantern-route", AdventureFixtures.Accepted));
        _rig.Messages.Post("narrator", "beat", "x", Noon, "the-lantern-route");

        Assert.Null(book.Abandon("F1", "the-lantern-route", AdventureFixtures.Accepted.AddDays(1)));

        Assert.Equal(0, Sweep());
        Assert.Single(_rig.Messages.All);
    }

    [Fact]
    public void ASweepWithNothingToRemoveDoesNotWriteOrRaiseChanged()
    {
        _rig.Messages.Post("narrator", "unkeyed", "x", Noon);

        var path = Path.Combine(_rig.Folder, "messages.json");
        var written = File.GetLastWriteTimeUtc(path);
        var raised = 0;
        _rig.Messages.Changed += () => raised++;

        Assert.Equal(0, Sweep());
        Assert.Equal(0, raised);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void ASweepThatRemovesRaisesChangedOnce()
    {
        _rig.Messages.Post("narrator", "a", "x", Noon, "gone");
        _rig.Messages.Post("narrator", "b", "x", Noon, "gone-too");

        var raised = 0;
        _rig.Messages.Changed += () => raised++;

        Assert.Equal(2, Sweep());
        Assert.Equal(1, raised);
    }
}
