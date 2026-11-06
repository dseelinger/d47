using System.Globalization;
using System.Text.Json;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The <c>get_faction_report</c> tool's answer and the phrases that reach it.</summary>
public sealed class TheFactionReportIsAnsweredWithoutAModelTests
{
    private const string Us = "Kestrel Reach Compact";

    private static readonly DateTimeOffset Reported = DateTimeOffset.Parse("2026-11-01T12:00:00Z", CultureInfo.InvariantCulture);

    private static FactionSystem Sys(
        string name,
        double? influence = 0.3,
        bool controls = false,
        string[]? active = null,
        string[]? pending = null,
        params FactionNeighbour[] others) =>
        new()
        {
            Name = name,
            Influence = influence,
            Controls = controls,
            ActiveStates = active ?? [],
            PendingStates = pending ?? [],
            Others = others,
            ReportedAt = Reported,
        };

    private static FactionNeighbour Other(string name, params string[] active) =>
        new(name, 0.1) { ActiveStates = active };

    private static FactionReport Report(params FactionSystem[] systems) => new(Us, "Confederacy", "Independent", systems, []);

    private sealed class Galaxy(FactionReport? report) : StubGalaxyService
    {
        public string? Asked { get; private set; }

        public override Task<FactionReport> FactionAsync(
            string faction,
            CancellationToken cancellationToken,
            bool refresh = false,
            IProgress<FactionProgress>? progress = null)
        {
            Asked = faction;

            return report is null
                ? throw new GalaxyUnavailableException("The galaxy service is not answering.")
                : Task.FromResult(report);
        }
    }

    private static CommanderGameState Squadron(bool member = true)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        if (member)
        {
            const string line =
                """{ "timestamp":"2026-10-19T12:00:00Z", "event":"SquadronStartup", "SquadronName":"GREYBEARD DELTA", "SquadronID":1234, "CurrentRank":3 }""";
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    private static async Task<string> Ask(Galaxy galaxy, CommanderGameState? state, string? faction = null, string? stored = null)
    {
        var squadronFactions = new SquadronFactions("squadron-factions.json", new MemoryFileSystem(), NullLogger<SquadronFactions>.Instance);

        if (state is not null && stored is not null)
        {
            Assert.True(squadronFactions.Set("F1", state.Squadron, stored));
        }

        var capability = GalaxyCapability.Create(
            galaxy,
            () => "Sol",
            TestSurface.For(new MemoryInstall()).Settings,
            gameState: () => state,
            squadronFactions: squadronFactions);

        var arguments = faction is null
            ? ToolArguments.Empty
            : new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["faction"] = faction });

        return (await capability.Tools.Single(tool => tool.Name == "get_faction_report")
            .Handler(arguments, TestContext.Current.CancellationToken)).Content;
    }

    private static string Describe(FactionReport report, SystemStandings? standings = null) =>
        FactionReportAnswer.Describe(report, standings ?? SystemStandings.Empty);

    [Fact]
    public async Task TheSquadronFactionIsAskedForWhenNoneIsNamed()
    {
        var galaxy = new Galaxy(Report(Sys("Alpha", controls: true)));

        var said = await Ask(galaxy, Squadron(), stored: Us);

        Assert.Equal(Us, galaxy.Asked);
        Assert.StartsWith($"{Us}: government Confederacy, allegiance Independent. Present in 1 system, controls 1.", said);
    }

    [Fact]
    public async Task ANamedFactionOverridesTheSquadronOne()
    {
        var galaxy = new Galaxy(Report());

        await Ask(galaxy, Squadron(), faction: "Hutton Truckers", stored: Us);

        Assert.Equal("Hutton Truckers", galaxy.Asked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoSquadronFactionAndNoNameSaysHowToGiveOne(bool member)
    {
        var said = await Ask(new Galaxy(Report()), Squadron(member));

        Assert.Equal(
            "D47 doesn't know your squadron's faction yet. Name one, or say 'my squadron's faction is' and its name.",
            said);
    }

    [Fact]
    public async Task SpanshBeingUnavailableIsReported()
    {
        var said = await Ask(new Galaxy(null), Squadron(), stored: Us);

        Assert.Equal("The galaxy service is not answering.", said);
    }

    [Fact]
    public void ConflictsNameThreeAndCountTheRestInTheOrderTheRowsGive()
    {
        var report = Report(
            Sys("Sol", active: ["War"], others: [Other("Hutton Truckers", "War")]),
            Sys("Alpha", active: ["Civil war"], others: [Other("Red", "Civil war"), Other("Blue", "Civil war"), Other("Green", "Civil war")]),
            Sys("Beta", active: ["Election"]),
            Sys("Gamma", pending: ["War"], others: [Other("Pink", "War")]),
            Sys("Delta", pending: ["Election"], others: [Other("Grey")]));

        Assert.Contains(
            "Conflicts: Civil war in Alpha against one of 3 factions; "
            + "Election in Beta, opponent not known yet; War in Sol against Hutton Truckers; and 2 more.",
            Describe(report));
    }

    [Fact]
    public void APendingConflictEndsWithPending()
    {
        var pink = new FactionNeighbour("Pink", 0.1) { PendingStates = ["War"] };
        var report = Report(Sys("Gamma", pending: ["War"], others: [pink]));

        Assert.Contains("Conflicts: War in Gamma against Pink, pending.", Describe(report));
    }

    [Fact]
    public void NoConflictsSaysSo()
    {
        Assert.Contains("No conflicts active or pending.", Describe(Report(Sys("Alpha"))));
    }

    [Fact]
    public void TheThreeLowestControlledInfluencesAreWholePercentsAndARowWithNoneIsLeftOut()
    {
        var report = Report(
            Sys("Alpha", 0.456, controls: true),
            Sys("Beta", 0.12, controls: true),
            Sys("Gamma", null, controls: true),
            Sys("Delta", 0.049, controls: true),
            Sys("Epsilon", 0.07),
            Sys("Zeta", 0.9, controls: true));

        Assert.Contains("Lowest influence where it controls: Delta 5%, Beta 12%, Alpha 46%.", Describe(report));
    }

    [Fact]
    public void RetreatAndExpansionAreCountedAndAZeroCountIsNotSaid()
    {
        var report = Report(
            Sys("Alpha", active: ["Retreat"]),
            Sys("Beta", pending: ["Retreat"]),
            Sys("Gamma", pending: ["Expansion"]));

        var said = Describe(report);

        Assert.Contains("Retreat in 2 systems.", said);
        Assert.Contains("Expansion in 1 system.", said);
        Assert.DoesNotContain("Retreat", Describe(Report(Sys("Alpha", pending: ["Expansion"]))));
        Assert.DoesNotContain("Expansion", Describe(Report(Sys("Alpha"))));
    }

    [Fact]
    public void ASystemSeenSinceSpanshsReportAnswersFromTheOwnReading()
    {
        var report = Report(Sys("Alpha", 0.5, controls: true));
        var at = "2026-11-02T09:00:00Z";
        using var document = JsonDocument.Parse(
            $"{{\"timestamp\":\"{at}\",\"event\":\"FSDJump\",\"StarSystem\":\"Alpha\",\"Factions\":[{{\"Name\":\"{Us}\",\"Influence\":0.21}}]}}");
        var standings = SystemStandings.Empty.Apply(
            new JournalEvent(DateTimeOffset.Parse(at, CultureInfo.InvariantCulture), "FSDJump", document.RootElement.Clone()));

        Assert.Contains("Alpha 21%", Describe(report, standings));
        Assert.Contains("Alpha 50%", Describe(report));
    }

    [Fact]
    public void AFactionWithNoSystemsSaysSo()
    {
        Assert.Equal($"The galaxy service lists no systems for {Us}.", Describe(Report()));
    }

    [Fact]
    public void ThePhraseReadsTheNameAndLeavesTheBareCommandToTheDeclaredPhrase()
    {
        Assert.Equal("Hutton Truckers", FactionReportPhrase.Read("faction report for Hutton Truckers"));
        Assert.Equal("Hutton Truckers", FactionReportPhrase.Read("Faction report for Hutton Truckers."));
        Assert.Null(FactionReportPhrase.Read("faction report"));
        Assert.Null(FactionReportPhrase.Read("faction report for"));
    }

    [Theory]
    [InlineData("faction report")]
    [InlineData("how is my squadron's faction doing")]
    public void EachDeclaredPhraseReachesTheToolAndNoOtherToolHasIt(string said)
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;

        var owners = registry.All
            .SelectMany(registered => registered.Descriptor.Tools)
            .Where(tool => tool.Commands.Any(command =>
                string.Equals(command.Phrase, said, StringComparison.OrdinalIgnoreCase)))
            .Select(tool => tool.Name);

        Assert.Equal(["get_faction_report"], owners);
        Assert.Equal("get_faction_report", new KeywordRouter(registry).MatchToolCommand(said)?.ToolName);
    }
}
