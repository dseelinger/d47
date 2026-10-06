using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary><c>describe_system</c> answers with a summary and names the system's page, or names close names and no page.</summary>
public class DescribeSystemAnswersAndOpensItsPageTests
{
    private const long Ltt7786 = 633608311522;

    private const long Sol = 10477373803;

    private static readonly StarSystemProfile Profile = new()
    {
        Name = "LTT 7786",
        SystemAddress = Ltt7786,
        Government = "Democracy",
        Allegiance = "Federation",
        PrimaryEconomy = "Industrial",
        Population = 1_250_000,
        ControllingFaction = "Fixture Party",
        Factions = [new FactionStanding("Fixture Party", "Democracy", "Federation", 0.6, ["Boom"], [])],
        Powerplay = new PowerplayStanding { ControllingPower = "Felicia Winters", State = "Fortified" },
        Stations =
        [
            new StationProfile { Name = "Fixture Port", Kind = StationKind.Starport },
            new StationProfile { Name = "Fixture Hold", Kind = StationKind.Outpost },
            new StationProfile { Name = "Fixture Yard", Kind = StationKind.Outpost },
        ],
        ReportedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
    };

    private sealed class FakeSystems : IStarSystemService
    {
        public Dictionary<long, StarSystemProfile> Profiles { get; } = [];

        public List<SystemNameMatch> Matches { get; } = [];

        public string? Typed { get; private set; }

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult(Profiles.GetValueOrDefault(systemAddress));

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken)
        {
            Typed = typed;
            return Task.FromResult<IReadOnlyList<SystemNameMatch>>(Matches);
        }

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(
            string system,
            double lightYears,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static SystemNameMatch Match(string name, long address) => new(name, address, new StarPosition(0, 0, 0));

    private static async Task<TurnResult> AskAsync(FakeSystems systems, string json, string input)
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
            [GalaxyCapability.Create(null, () => "Sol", settings, systems: systems, currentAddress: () => Sol)]);

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Calling("call_1", "describe_system", json),
                RoundScriptedLlmProvider.Saying("There.")),
            clock: new InstantClock())
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
        };

        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        Assert.Equal(TurnRoute.Model, result.Route);
        return result;
    }

    private static Task<ToolResult> CallAsync(FakeSystems systems, Dictionary<string, string> arguments)
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var tool = GalaxyCapability.Create(null, () => "Sol", settings, systems: systems, currentAddress: () => Sol)
            .Tools.Single(tool => tool.Name == "describe_system");

        return tool.Handler(new ToolArguments(arguments), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TellMeAboutANamedSystemOpensItsPage()
    {
        var systems = new FakeSystems();
        systems.Matches.Add(Match("LTT 7786", Ltt7786));
        systems.Profiles[Ltt7786] = Profile;

        var result = await AskAsync(systems, """{"system":"LTT 7786"}""", "tell me about LTT 7786");

        Assert.Equal(PageRef.System(Ltt7786), result.Page);
    }

    [Fact]
    public async Task TheSummarySaysWhatTheIssueLists()
    {
        var systems = new FakeSystems();
        systems.Matches.Add(Match("LTT 7786", Ltt7786));
        systems.Profiles[Ltt7786] = Profile;

        var result = await CallAsync(systems, new() { ["system"] = "ltt 7786" });

        Assert.False(result.IsError);
        Assert.Equal(
            "LTT 7786: Democracy, Federation, Industrial economy, population 1,250,000. Controlled by Fixture Party, "
            + "in Boom. Powerplay: Felicia Winters, Fortified. Stations: 1 starport, 2 outposts. "
            + "Spansh last had a report on 1 October 2026.",
            result.Content);
        Assert.Equal(PageRef.System(Ltt7786), result.Page);
    }

    [Fact]
    public async Task NoSystemNamedDescribesTheCommandersOwn()
    {
        var systems = new FakeSystems();
        systems.Profiles[Sol] = Profile with { Name = "Sol", SystemAddress = Sol };

        var result = await CallAsync(systems, []);

        Assert.StartsWith("Sol:", result.Content, StringComparison.Ordinal);
        Assert.Null(systems.Typed);
        Assert.Equal(PageRef.System(Sol), result.Page);
    }

    [Fact]
    public async Task ANameWithNoExactMatchSaysTheCloseNamesAndOpensNothing()
    {
        var systems = new FakeSystems();
        systems.Matches.AddRange(
        [
            Match("LTT 7787", 1),
            Match("LTT 7788", 2),
            Match("LTT 7789", 3),
            Match("LTT 7790", 4),
        ]);

        var result = await CallAsync(systems, new() { ["system"] = "LTT 7786" });

        Assert.True(result.IsError);
        Assert.Equal(
            "Spansh has no system named exactly LTT 7786. Close names: LTT 7787, LTT 7788, LTT 7789.",
            result.Content);
        Assert.Null(result.Page);
    }

    [Fact]
    public async Task ASystemSpanshHasNoRecordOfOpensNothing()
    {
        var systems = new FakeSystems();
        systems.Matches.Add(Match("LTT 7786", Ltt7786));

        var result = await CallAsync(systems, new() { ["system"] = "LTT 7786" });

        Assert.True(result.IsError);
        Assert.Equal("Spansh has no record of LTT 7786.", result.Content);
        Assert.Null(result.Page);
    }
}
