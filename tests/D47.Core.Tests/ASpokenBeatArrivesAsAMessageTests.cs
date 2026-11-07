using System.Text.Json.Nodes;
using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests;

[Trait("Category", "Integration")]
public class ASpokenBeatArrivesAsAMessageTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static Adventure Story() => new()
    {
        Key = "salvage",
        Name = "The Salvage",
        Beats =
        [
            new AdventureBeat
            {
                Title = "The Wreck",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 10477373803 },
                Line = "There it is.",
            },
        ],
    };

    [Fact]
    public void ABeatIsPostedUnderItsTitleWithTheStoryKey()
    {
        var store = new MessageStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);

        AdventureMessages.Post(store, "narrator", Story(), "salvage", 0, "There it is.", Noon);

        var message = Assert.Single(store.All);
        Assert.Equal("The Wreck", message.Subject);
        Assert.Equal("There it is.", message.Body);
        Assert.Equal("salvage", message.AdventureKey);
        Assert.Equal("narrator", message.From);
        Assert.False(message.Read);
    }

    [Fact]
    public void TheOpeningIsPostedUnderTheStoryName()
    {
        var store = new MessageStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"), new MemoryFileSystem(), NullLogger<MessageStore>.Instance);

        AdventureMessages.Post(store, "narrator", Story(), "salvage", -1, "It begins.", Noon);

        Assert.Equal("The Salvage", Assert.Single(store.All).Subject);
    }

    [Fact]
    public void TheOldestReadMessageGoesFirstWhenTheStoreIsFull()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        var files = new MemoryFileSystem();
        var posting = new MessageStore(path, files, NullLogger<MessageStore>.Instance);
        var first = posting.Post("narrator", "a", "a", Noon);
        var second = posting.Post("narrator", "b", "b", Noon.AddMinutes(1));

        var saved = JsonNode.Parse(files.ReadText(path)!)!.AsArray();
        saved.Single(node => node!["key"]!.GetValue<string>() == second.Key)!["read"] = true;
        files.WriteText(path, saved.ToJsonString());

        var store = new MessageStore(path, files, NullLogger<MessageStore>.Instance);

        for (var i = 0; i < MessageStore.Capacity - 1; i++)
        {
            store.Post("narrator", "x", "x", Noon.AddMinutes(2 + i));
        }

        Assert.Equal(MessageStore.Capacity, store.All.Count);
        Assert.Contains(store.All, message => message.Key == first.Key);
        Assert.DoesNotContain(store.All, message => message.Key == second.Key);
    }
}
