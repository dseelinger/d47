using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

public class AModuleLineIsCappedBeforeItIsQueuedTests
{
    [Fact]
    public void TheTextIsCutAtThreeHundredCharacters()
    {
        var inbox = new ModuleInbox();

        inbox.Post(new ModuleLine("ledger", "tally", new string('a', 1000)));

        var line = Assert.IsType<ModuleLine>(Assert.Single(inbox.Drain()));
        Assert.Equal(300, line.Text.Length);
    }

    [Fact]
    public void AShortLineIsKeptWhole()
    {
        var line = new ModuleLine("ledger", "tally", "Three bounties open.");

        Assert.Equal("Three bounties open.", line.Text);
        Assert.Equal("tally", line.Key);
    }

    [Fact]
    public void DrainingEmptiesTheInboxInOrder()
    {
        var inbox = new ModuleInbox();

        inbox.Post(new ModuleLine("ledger", "a", "one"));
        inbox.Post(new ModuleResourceUpdated("ledger", "ledger://open"));

        Assert.Equal(
            [new ModuleLine("ledger", "a", "one"), new ModuleResourceUpdated("ledger", "ledger://open")],
            inbox.Drain());
        Assert.Empty(inbox.Drain());
    }

    [Fact]
    public void AFullInboxDropsTheOldest()
    {
        var inbox = new ModuleInbox();

        for (var i = 0; i < ModuleInbox.Capacity + 10; i++)
        {
            inbox.Post(new ModuleResourceUpdated("ledger", $"ledger://{i}"));
        }

        var drained = inbox.Drain();

        Assert.Equal(ModuleInbox.Capacity, drained.Count);
        Assert.Equal(new ModuleResourceUpdated("ledger", "ledger://10"), drained[0]);
    }
}
