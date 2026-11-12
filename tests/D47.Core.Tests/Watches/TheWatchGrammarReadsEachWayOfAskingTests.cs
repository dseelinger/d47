using D47.Core.Knowledge;
using D47.Core.Watches;
using Xunit;

namespace D47.Core.Tests.Watches;

public class TheWatchGrammarReadsEachWayOfAskingTests
{
    [Theory]
    [InlineData("Tell me the next time we're in a system with a material trader")]
    [InlineData("tell me the next time we are in a system with a material trader.")]
    [InlineData("Let me know the next time I'm in a system with a material trader")]
    [InlineData("let me know the next time I am in a system with a material trader")]
    [InlineData("please tell me next time we’re in a system with a materials trader")]
    [InlineData("tell me when we arrive in a system with a mats trader")]
    public void EachOpenerAndEachWayOfSayingMaterialTraderAddsOne(string said)
    {
        var added = Assert.IsType<WatchReading.Add>(WatchPhrase.Read(said));

        Assert.Equal("Material Trader", added.Service);
        Assert.Null(added.Kind);
        Assert.Null(added.Pad);
    }

    [Theory]
    [InlineData("black market", "Black Market")]
    [InlineData("an Interstellar Factors", "Interstellar Factors")]
    [InlineData("outfitting", "Outfitting")]
    [InlineData("a refuel", "Refuel")]
    [InlineData("repair", "Repair")]
    [InlineData("a shipyard", "Shipyard")]
    [InlineData("a technology broker", "Technology Broker")]
    [InlineData("a tech broker", "Technology Broker")]
    [InlineData("universal cartographics", "Universal Cartographics")]
    [InlineData("vista genomics", "Vista Genomics")]
    public void EachServiceIsReadByItsName(string service, string expected)
    {
        var added = Assert.IsType<WatchReading.Add>(WatchPhrase.Read($"tell me the next time we're in a system with {service}"));

        Assert.Equal(expected, added.Service);
        Assert.Null(added.Kind);
    }

    [Theory]
    [InlineData("a manufactured material trader", "Manufactured")]
    [InlineData("a raw materials trader", "Raw")]
    [InlineData("an encoded mats trader", "Encoded")]
    [InlineData("a material trader for encoded", "Encoded")]
    [InlineData("a material trader for manufactured materials", "Manufactured")]
    public void AKindComesBeforeOrAfterAMaterialTrader(string service, string kind)
    {
        var added = Assert.IsType<WatchReading.Add>(WatchPhrase.Read($"let me know the next time we're in a system with {service}"));

        Assert.Equal("Material Trader", added.Service);
        Assert.Equal(kind, added.Kind);
    }

    [Theory]
    [InlineData("a guardian tech broker", "Guardian")]
    [InlineData("a technology broker for human", "Human")]
    public void AKindComesBeforeOrAfterATechnologyBroker(string service, string kind)
    {
        var added = Assert.IsType<WatchReading.Add>(WatchPhrase.Read($"tell me the next time we're in a system with {service}"));

        Assert.Equal("Technology Broker", added.Service);
        Assert.Equal(kind, added.Kind);
    }

    [Theory]
    [InlineData("a material trader with a large pad", PadSize.Large)]
    [InlineData("a material trader that has a medium pad", PadSize.Medium)]
    [InlineData("a manufactured material trader, with a large landing pad", PadSize.Large)]
    public void APadIsReadFromTheEnd(string service, PadSize pad)
    {
        var added = Assert.IsType<WatchReading.Add>(WatchPhrase.Read($"tell me the next time we're in a system with {service}"));

        Assert.Equal("Material Trader", added.Service);
        Assert.Equal(pad, added.Pad);
    }

    [Fact]
    public void ThePadAndTheKindAreBothKept()
    {
        var added = Assert.IsType<WatchReading.Add>(
            WatchPhrase.Read("tell me the next time we're in a system with a manufactured material trader with a large pad"));

        Assert.Equal(new WatchReading.Add("Material Trader", "Manufactured", PadSize.Large), added);
    }

    [Theory]
    [InlineData("stop watching for a material trader", "Material Trader", null)]
    [InlineData("Stop watching for a manufactured material trader.", "Material Trader", "Manufactured")]
    [InlineData("delete the watch for a guardian technology broker", "Technology Broker", "Guardian")]
    [InlineData("stop watching for interstellar factors", "Interstellar Factors", null)]
    public void TheStopFormDeletes(string said, string service, string? kind)
    {
        Assert.Equal(new WatchReading.Delete(service, kind), WatchPhrase.Read(said));
    }

    [Theory]
    [InlineData("tell me the next time we're in a system with a coffee shop", "a coffee shop")]
    [InlineData("let me know the next time I'm in a system with an anarchy faction", "an anarchy faction")]
    [InlineData("stop watching for pirates", "pirates")]
    public void AnUnknownServiceIsDeclinedWithTheWordsSaid(string said, string words)
    {
        Assert.Equal(new WatchReading.Declined(words), WatchPhrase.Read(said));
    }

    [Theory]
    [InlineData("")]
    [InlineData("remind me to buy limpets when I next dock")]
    [InlineData("tell me the next time we dock")]
    [InlineData("list my watches")]
    [InlineData("what is the nearest material trader")]
    public void OtherSentencesAreLeftAlone(string said)
    {
        Assert.Null(WatchPhrase.Read(said));
    }

    [Fact]
    public void ADeclinedReplyNamesThePhraseAndWhatCanBeWatched()
    {
        Assert.Equal(
            "I can watch for a station service, Commander, such as a material trader, a technology broker or Interstellar Factors. A coffee shop is not one of them.",
            WatchVoice.Declined("a coffee shop"));
    }
}
