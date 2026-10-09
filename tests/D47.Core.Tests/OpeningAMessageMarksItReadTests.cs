using D47.Core.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests;

[Trait("Category", "Integration")]
public class OpeningAMessageMarksItReadTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MarkingReadDropsTheUnreadCountAndSurvivesAReload()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var store = new MessageStore(path, NullLogger<MessageStore>.Instance);
        var message = store.Post("narrator", "Subject", "Body", Noon);

        Assert.Equal(1, store.UnreadCount);
        Assert.True(store.MarkRead(message.Key));
        Assert.False(store.MarkRead(message.Key));
        Assert.Equal(0, store.UnreadCount);

        var reloaded = new MessageStore(path, NullLogger<MessageStore>.Instance);
        Assert.True(Assert.Single(reloaded.All).Read);
    }

    [Fact]
    public void MessagesComeNewestFirst()
    {
        var store = new MessageStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"), NullLogger<MessageStore>.Instance);
        store.Post("narrator", "old", "x", Noon);
        store.Post("narrator", "new", "x", Noon.AddHours(1));

        Assert.Equal(["new", "old"], store.All.Select(message => message.Subject));
    }
}
