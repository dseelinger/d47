using D47.Core.Messages;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests;

public class MessagesComeNewestFirstTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MessagesComeNewestFirst()
    {
        var store = new MessageStore(Path.Combine(MemoryInstall.FakeRoot, Guid.NewGuid().ToString("N") + ".json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);
        store.Post("narrator", "old", "x", Noon);
        store.Post("narrator", "new", "x", Noon.AddHours(1));

        Assert.Equal(["new", "old"], store.All.Select(message => message.Subject));
    }
}
