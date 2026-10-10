using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The legal state and the unpaid fines and bounties, read out by one tool (#684).</summary>
public class AskedWhatTheCommanderOwesTheLedgerIsReadOutTests
{
    private const string Fid = "F1234";

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Crime(string faction, string type, string amount, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"CommitCrime","CrimeType":"{{type}}","Faction":"{{faction}}",{{amount}}}""";

    private static string PayBounties(string faction, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"PayBounties","Amount":9062,"AllFines":false,"Faction":"{{faction}}","ShipID":4293000001}""";

    private const string Commander = $$"""{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"{{Fid}}","Name":"Doug"}""";

    private const string Load =
        $$"""{"timestamp":"2026-09-05T10:00:01Z","event":"LoadGame","FID":"{{Fid}}","Commander":"Doug","Ship":"Python","ShipID":2}""";

    private const string Statistics =
        """{"timestamp":"2026-09-05T10:00:02Z","event":"Statistics","Crime":{"Notoriety":3,"Fines":1}}""";

    private static async Task<ToolResult> Ask(string[] journal, GameStatus? status = null)
    {
        var gameState = new GameStateStore();
        var crimes = new OutstandingCrimes(new MemoryFileSystem(), NullLogger.Instance);

        foreach (var line in journal)
        {
            var parsed = Parse(line);
            gameState.Apply(parsed);
            crimes.Apply([parsed]);
        }

        crimes.FoldHistory([]);

        var registry = CapabilityRegistry.Build(
            [JournalCapability.Create(gameState, crimes: crimes, liveStatus: () => status ?? new GameStatus())]);

        return await registry.InvokeAsync("get_crime_status", ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnOnFootBountyAndFineNameTheFactionBothAmountsAndTheDate()
    {
        var result = await Ask(
            [
                Commander, Load, Statistics,
                Crime("Turner Research Group", "onFoot_murder", "\"Bounty\":1000", "10:05:00"),
                Crime("Turner Research Group", "onFoot_assault", "\"Fine\":400", "10:06:00"),
            ],
            new GameStatus { LegalState = "Wanted", Flags2 = (uint)StatusFlags2.OnFoot });

        Assert.Contains("Legal state now: Wanted, on foot.", result.Content, StringComparison.Ordinal);
        Assert.Contains(
            "Turner Research Group: 400 credits in fines and 1,000 credits in bounties on foot, newest crime 2026-09-05",
            result.Content,
            StringComparison.Ordinal);
        Assert.Contains("Notoriety 3", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PayingTheBountiesLeavesNothingOwed()
    {
        var result = await Ask(
            [
                Commander, Load,
                Crime("Turner Research Group", "onFoot_murder", "\"Bounty\":1000", "10:05:00"),
                Crime("Turner Research Group", "onFoot_assault", "\"Fine\":400", "10:06:00"),
                PayBounties("Turner Research Group", "10:30:00"),
            ]);

        Assert.Contains("nothing owed", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Turner Research Group", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiveFactionsOwedAreSpokenAsThreeNamesAndACountOfTwo()
    {
        var factions = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo" };

        var result = await Ask(
            [Commander, Load, .. factions.Select((name, index) =>
                Crime(name, "dumpingDangerous", $"\"Fine\":{(5 - index) * 100}", $"10:0{index + 1}:00"))]);

        Assert.Equal(3, factions.Count(name => result.Spoken.Contains(name, StringComparison.Ordinal)));
        Assert.Contains(", and 2 more.", result.Spoken, StringComparison.Ordinal);
        Assert.Equal(5, factions.Count(name => result.Content.Contains(name, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task WithNoStatisticsEventNotorietyIsLeftOutRatherThanZero()
    {
        var result = await Ask([Commander, Load, Crime("Alpha", "dumpingDangerous", "\"Fine\":100", "10:05:00")]);

        Assert.DoesNotContain("Notoriety", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnotherShipsDebtIsCountedNotNamed()
    {
        var result = await Ask(
            [
                Commander, Load,
                Crime("Alpha", "dumpingDangerous", "\"Fine\":100", "10:05:00"),
                """{"timestamp":"2026-09-05T10:10:00Z","event":"ShipyardSwap","ShipType":"cobramkiii","ShipID":7,"StoreOldShip":"Python","StoreShipID":2,"MarketID":1}""",
            ]);

        Assert.Contains("1 other ship carries unpaid debts.", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("crime status")]
    [InlineData("am I wanted")]
    [InlineData("my bounties")]
    [InlineData("my fines")]
    [InlineData("what do I owe")]
    public void ItIsReachedWithoutTheModel(string said)
    {
        var install = new MemoryInstall();
        var command = new KeywordRouter(TestSurface.For(install).Registry).MatchToolCommand(said);

        Assert.Equal("get_crime_status", command?.ToolName);
    }
}
