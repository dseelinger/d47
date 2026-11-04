using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class PassersByTalkAboutTheLocalEconomyTests
{
    private static JournalEvent Event(string kind, string fields) =>
        new(new DateTimeOffset(3311, 1, 1, 12, 0, 0, TimeSpan.Zero), kind,
            JsonDocument.Parse($"{{ \"event\": \"{kind}\", {fields} }}").RootElement);

    private const string Jump =
        "\"StarSystem\": \"Sol\", \"SystemAddress\": 1, \"SystemEconomy_Localised\": \"High Tech\", "
        + "\"SystemFaction\": { \"Name\": \"Sol Gov\", \"FactionState\": \"Boom\" }";

    private static JournalLocation Known => JournalLocation.Unknown.Apply(Event("FSDJump", Jump));

    [Fact]
    public void AJumpIntoASystemSetsItsEconomyAndFactionStateAndALaterBareJumpClearsThem()
    {
        var at = Known;
        Assert.Equal("High Tech", at.SystemEconomy);
        Assert.Equal("Boom", at.SystemFactionState);

        var bare = at.Apply(Event("FSDJump", "\"StarSystem\": \"Wolf\", \"SystemAddress\": 2"));
        Assert.Null(bare.SystemEconomy);
        Assert.Null(bare.SystemFactionState);
    }

    [Fact]
    public void AFactionStateOfNoneCountsAsAbsent()
    {
        var at = JournalLocation.Unknown.Apply(Event("FSDJump",
            "\"StarSystem\": \"Sol\", \"SystemFaction\": { \"Name\": \"Sol Gov\", \"FactionState\": \"None\" }"));
        Assert.Null(at.SystemFactionState);
    }

    [Fact]
    public void DockingSetsTheStationEconomyAndUndockingClearsIt()
    {
        var docked = Known.Apply(Event("Docked", "\"StationName\": \"Hub\", \"StationEconomy_Localised\": \"Extraction\""));
        Assert.Equal("Extraction", docked.StationEconomy);
        Assert.Null(docked.Apply(Event("Undocked", "\"StationName\": \"Hub\"")).StationEconomy);
    }

    [Fact]
    public void ALocationEventSetsTheStationEconomyOnlyWhenDocked()
    {
        var fields = "\"StarSystem\": \"Sol\", \"StationEconomy_Localised\": \"Refinery\", \"Docked\": ";
        Assert.Equal("Refinery", JournalLocation.Unknown.Apply(Event("Location", fields + "true")).StationEconomy);
        Assert.Null(JournalLocation.Unknown.Apply(Event("Location", fields + "false")).StationEconomy);
    }

    [Fact]
    public void AKnownEconomyIsNamedInOneExchangeInThreeAndNeverOtherwise()
    {
        var place = Known with { SystemFactionState = null, SystemFaction = null };
        var named = Enumerable.Range(0, 300)
            .Count(index => NpcChatter.Instruction(NpcChatterKind.Passersby, exchangeIndex: index, place: place)
                .Contains("the High Tech trade here", StringComparison.Ordinal));

        Assert.InRange(named, 60, 140);

        foreach (var index in Enumerable.Range(0, 300))
        {
            var text = NpcChatter.Instruction(NpcChatterKind.Passersby, exchangeIndex: index);
            Assert.DoesNotContain("trade here", text, StringComparison.Ordinal);
            Assert.Equal(
                NpcChatter.Instruction(NpcChatterKind.Passersby, exchangeIndex: index),
                NpcChatter.Instruction(NpcChatterKind.Passersby, exchangeIndex: index, place: JournalLocation.Unknown));
        }
    }

    [Fact]
    public void AFactionStateIsWordedAsLivingWithIt()
    {
        var place = JournalLocation.Unknown with { SystemFactionState = "Famine" };
        Assert.Contains(
            Enumerable.Range(0, 100).Select(index => NpcChatter.LocalTopic(index, place)),
            topic => topic == "living with the Famine the local faction is in");
    }

    [Fact]
    public void ADockedHailCanBeAboutTheLocalEconomy()
    {
        var place = Known with { SystemFactionState = null, SystemFaction = null };
        Assert.Contains(
            Enumerable.Range(0, 100).Select(index =>
                NpcChatter.Instruction(NpcChatterKind.Hail, docked: true, exchangeIndex: index, place: place)),
            text => text.Contains("the High Tech trade here", StringComparison.Ordinal));
    }
}
