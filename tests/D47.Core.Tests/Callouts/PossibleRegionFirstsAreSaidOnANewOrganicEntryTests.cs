using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A new organic codex entry the Canonn table does not list for the region is said once.</summary>
public class PossibleRegionFirstsAreSaidOnANewOrganicEntryTests
{
    private const long Listed = 2100101;
    private const long Unlisted = 2100102;
    private const long Unknown = 2100103;

    private static readonly CodexTable Table = CodexTable.Parse(
        "2100101\tFonticulua Fluctus - Yellow\tBiology\tOrganic\t18:5\n"
        + "2100102\tFonticulua Fluctus - Lime\tBiology\tOrganic\t3:2\n"
        + "2100104\tNo Regions\tBiology\tOrganic\t\n");

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Entry(
        long id,
        string stamp = "2026-08-16T10:00:00Z",
        string sub = "$Codex_SubCategory_Organic_Structures;",
        bool isNew = true) => $$"""
        {"timestamp":"{{stamp}}","event":"CodexEntry","EntryID":{{id}},"Name":"$Codex_Ent_{{id}};",
         "Name_Localised":"Fonticulua Fluctus - Yellow","SubCategory":"{{sub}}","Region":"$Codex_RegionName_18;",
         "Region_Localised":"Inner Orion Spur","BodyID":3{{(isNew ? ",\"IsNewEntry\":true" : "")}}}
        """;

    private static CommanderGameState Commander(bool whole, params string[] events)
    {
        var store = new GameStateStore();
        store.Apply(Parse("""{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var json in events)
        {
            store.Apply(Parse(json));
        }

        var state = store.Active!;
        state.CodexLedger = state.CodexLedger with { IsWhole = whole };

        return state;
    }

    private static List<Announcement> Say(
        RegionFirstCallout callout, CommanderGameState state, bool priming, params string[] lines) =>
        [
            .. callout.Examine(new CalloutContext(
                DateTimeOffset.UnixEpoch,
                IsPriming: priming,
                State: state,
                Status: GameStatus.Unknown,
                Route: NavRoute.None,
                Events: [.. lines.Select(Parse)])),
        ];

    private static RegionFirstCallout Callout() => new() { Table = () => Table };

    [Fact]
    public void AnEntryTheTableDoesNotListForTheRegionGivesTheRegionLine()
    {
        var line = Entry(Unlisted);
        var said = Assert.Single(Say(Callout(), Commander(true, line), false, line));

        Assert.Equal(
            "Canonn has no report of Fonticulua Fluctus - Yellow in the Inner Orion Spur. It may be a first for the region.",
            said.Text);
    }

    [Theory]
    [InlineData(Unknown)]
    [InlineData(2100104)]
    public void AnEntryTheTableLacksOrListsNoRegionsForGivesTheGalaxyLine(long id)
    {
        var line = Entry(id);
        var said = Assert.Single(Say(Callout(), Commander(true, line), false, line));

        Assert.Equal(
            "Canonn has no report of Fonticulua Fluctus - Yellow anywhere. It may be a first in the galaxy.",
            said.Text);
    }

    [Fact]
    public void AnEntryWhoseRegionIsListedSaysNothing()
    {
        var line = Entry(Listed);

        Assert.Empty(Say(Callout(), Commander(true, line), false, line));
    }

    [Fact]
    public void ALineWithoutIsNewEntrySaysNothing()
    {
        var line = Entry(Unknown, isNew: false);

        Assert.Empty(Say(Callout(), Commander(true, line), false, line));
    }

    [Fact]
    public void AGeologyEntrySaysNothing()
    {
        var line = Entry(Unknown, sub: "$Codex_SubCategory_Geology_Structures;");

        Assert.Empty(Say(Callout(), Commander(true, line), false, line));
    }

    [Fact]
    public void ARepeatOfTheSameEntryAndRegionInTheSessionSaysNothing()
    {
        var first = Entry(Unknown);
        var again = Entry(Unknown, "2026-08-16T10:30:00Z");
        var callout = Callout();
        var state = Commander(true, first, again);

        Assert.Single(Say(callout, state, false, first));
        Assert.Empty(Say(callout, state, false, again));
    }

    [Fact]
    public void AnEntryTheLedgerHeldFromEarlierForThatRegionSaysNothing()
    {
        var earlier = Entry(Unknown, "2026-08-10T10:00:00Z");
        var line = Entry(Unknown);

        Assert.Empty(Say(Callout(), Commander(true, earlier, line), false, line));
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var line = Entry(Unknown);

        Assert.Empty(Say(Callout(), Commander(true, line), true, line));
    }
}
