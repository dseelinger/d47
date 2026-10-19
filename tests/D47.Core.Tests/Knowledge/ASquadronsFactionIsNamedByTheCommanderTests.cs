using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The Commander names their squadron's minor faction, d47 checks it with the galaxy service and keeps it per squadron (#1308).</summary>
public class ASquadronsFactionIsNamedByTheCommanderTests
{
    private const string Commander = "F1";

    private const string Startup = "\"SquadronName\":\"GREYBEARD DELTA\", \"SquadronID\":1234";

    private readonly CommanderGameState _state = new(new CommanderIdentity(Commander, "Fixture"));

    private readonly MemoryFileSystem _files = new();

    private readonly SquadronFactions _store;

    private readonly Galaxy _galaxy = new();

    private readonly SettingsService _settings;

    public ASquadronsFactionIsNamedByTheCommanderTests()
    {
        _store = new SquadronFactions("squadron-factions.json", _files, NullLogger<SquadronFactions>.Instance);
        _settings = TestSurface.For(new MemoryInstall()).Settings;
        _settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
    }

    private sealed class Galaxy : IGalaxyService
    {
        public HashSet<string> Known { get; } = new(StringComparer.OrdinalIgnoreCase) { "Hutton Truckers" };

        public bool Down { get; set; }

        public int Searches { get; private set; }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Searches++;

            if (Down)
            {
                throw new GalaxyUnavailableException("The galaxy service is not answering.");
            }

            var name = Assert.Single(Assert.Single(query.Criteria).Choices);

            return Task.FromResult(Known.TryGetValue(name, out var spelled)
                ? new GalaxySearchResult(null, 1, [new SystemSummary { Name = "Hutton Orbital", Factions = [new FactionPresence(spelled, 0.4)] }])
                : new GalaxySearchResult(null, 0, []));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static JournalEvent Event(string kind, string fields)
    {
        var line = "{ \"timestamp\":\"2026-10-19T12:00:00Z\", \"event\":\"" + kind + "\", " + fields + " }";
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private void Journal(string kind, string fields) => _state.Apply(Event(kind, fields));

    private CapabilityRegistry Registry() =>
        CapabilityRegistry.Build(
            [GalaxyCapability.Create(_galaxy, () => "Sol", _settings, gameState: () => _state, squadronFactions: _store)]);

    private async Task<(string Said, int ModelCalls, TurnRoute? Route)> SaidAsync(string input)
    {
        var registry = Registry();
        var model = FakeLlmProvider.Answering("From the model.");
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            model,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };

        var text = new System.Text.StringBuilder();
        TurnRoute? route = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.TextDelta delta)
            {
                text.Append(delta.Text);
            }
            else if (turnEvent is TurnEvent.Routed routed)
            {
                route = routed.Route;
            }
        }

        return (text.ToString(), model.CallCount, route);
    }

    private string? Stored => _store.For(Commander, _state.Squadron);

    [Fact]
    public void ASquadronKeepsItsIdFromTheEventsThatCarryOne()
    {
        var state = SquadronState.None.Apply(Event("SquadronStartup", Startup + ", \"CurrentRank\":3"));

        Assert.Equal(1234, state.SquadronId);
        Assert.Equal("GREYBEARD DELTA", state.Name);
        Assert.Equal("1234", state.Key);

        state = SquadronState.None.Apply(Event("JoinedSquadron", "\"SquadronName\":\"X\", \"SquadronID\":77"));
        Assert.Equal(77, state.SquadronId);
    }

    [Fact]
    public void AnEventWithNoIdKeepsTheNameAndFallsBackToItAsTheKey()
    {
        var state = SquadronState.None.Apply(Event("SquadronStartup", "\"SquadronName\":\"GREYBEARD DELTA\", \"CurrentRank\":3"));

        Assert.Null(state.SquadronId);
        Assert.Equal("GREYBEARD DELTA", state.Name);
        Assert.Equal("GREYBEARD DELTA", state.Key);
        Assert.Null(SquadronState.None.Key);
    }

    [Fact]
    public async Task MySquadronsFactionIsStoredWithoutReachingTheModel()
    {
        Journal("SquadronStartup", Startup);

        var (said, modelCalls, route) = await SaidAsync("my squadron's faction is hutton truckers");

        Assert.Equal(0, modelCalls);
        Assert.Equal(TurnRoute.ActionCommand, route);
        Assert.Equal("Your squadron's minor faction is Hutton Truckers.", said);
        Assert.Equal("Hutton Truckers", Stored);
    }

    [Theory]
    [InlineData("our squadron's minor faction is Hutton Truckers.")]
    [InlineData("my squadrons faction is called Hutton Truckers")]
    public async Task EitherWordingNamesTheFaction(string input)
    {
        Journal("SquadronStartup", Startup);

        await SaidAsync(input);

        Assert.Equal("Hutton Truckers", Stored);
    }

    [Fact]
    public async Task TheModelCannotStoreAFaction()
    {
        Journal("SquadronStartup", Startup);

        var result = await Registry().InvokeAsync(
            GalaxyCapability.SetSquadronFactionTool,
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["faction"] = "Hutton Truckers" }),
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.Contains("my squadron's faction is", result.Content, StringComparison.Ordinal);
        Assert.Null(Stored);
        Assert.Equal(0, _galaxy.Searches);
    }

    [Fact]
    public async Task ANameTheGalaxyServiceDoesNotKnowIsNotStored()
    {
        Journal("SquadronStartup", Startup);

        var (said, _, _) = await SaidAsync("my squadron's faction is Nobody At All");

        Assert.Contains("found no minor faction called Nobody At All", said, StringComparison.Ordinal);
        Assert.Null(Stored);
    }

    [Fact]
    public async Task WithGalaxySearchOffTheNameIsRefusedAndSaysWhy()
    {
        _settings.Apply(GalaxyCapability.EnabledKey, "false", SettingsCaller.Panel);
        Journal("SquadronStartup", Startup);

        var (said, _, _) = await SaidAsync("my squadron's faction is Hutton Truckers");

        Assert.Contains("needs galaxy search", said, StringComparison.Ordinal);
        Assert.Equal(0, _galaxy.Searches);
        Assert.Null(Stored);
    }

    [Fact]
    public async Task AServiceThatIsDownStoresNothing()
    {
        _galaxy.Down = true;
        Journal("SquadronStartup", Startup);

        var (said, _, _) = await SaidAsync("my squadron's faction is Hutton Truckers");

        Assert.Equal("The galaxy service is not answering.", said);
        Assert.Null(Stored);
    }

    [Fact]
    public async Task OutsideASquadronThereIsNothingToKeepItFor()
    {
        var (said, _, _) = await SaidAsync("my squadron's faction is Hutton Truckers");

        Assert.Contains("not in a squadron", said, StringComparison.Ordinal);
        Assert.Equal(0, _galaxy.Searches);
    }

    [Fact]
    public async Task LeavingOrChangingSquadronReturnsNoFactionUntilOneIsNamedForTheNewSquadron()
    {
        Journal("SquadronStartup", Startup);
        await SaidAsync("my squadron's faction is Hutton Truckers");
        Assert.Equal("Hutton Truckers", Stored);

        Journal("LeftSquadron", "\"SquadronName\":\"GREYBEARD DELTA\"");
        Assert.Null(Stored);

        Journal("JoinedSquadron", "\"SquadronName\":\"OTHER\", \"SquadronID\":99");
        Assert.Null(Stored);

        Journal("LeftSquadron", "\"SquadronName\":\"OTHER\"");
        Journal("JoinedSquadron", Startup);
        Assert.Equal("Hutton Truckers", Stored);
    }

    [Fact]
    public async Task ASquadronWithNoIdIsKeyedByItsName()
    {
        Journal("SquadronStartup", "\"SquadronName\":\"OLD SQUADRON\"");

        await SaidAsync("my squadron's faction is Hutton Truckers");

        Assert.Equal("Hutton Truckers", Stored);
        Assert.Contains("OLD SQUADRON", _files.ReadText("squadron-factions.json"), StringComparison.Ordinal);
    }

    [Fact]
    public void AFactionNamedFromAJoinIsFoundAgainAfterAStartupWithNoId()
    {
        var joined = SquadronState.None.Apply(Event("JoinedSquadron", Startup));
        var later = SquadronState.None.Apply(Event("SquadronStartup", "\"SquadronName\":\"GREYBEARD DELTA\""));

        _store.Set(Commander, joined, "Hutton Truckers");

        Assert.Equal("Hutton Truckers", _store.For(Commander, later));
    }

    [Fact]
    public void ANameStartingWithTheKeepsIt()
    {
        Assert.Equal("The Fuel Rats", SquadronFactionPhrase.Read("my squadron's faction is The Fuel Rats"));
    }

    [Fact]
    public void ASentenceThatIsNotTheGrammarIsLeftAlone()
    {
        Assert.Null(SquadronFactionPhrase.Read("what is my squadron's faction"));
        Assert.Null(SquadronFactionPhrase.Read("my faction is Hutton Truckers"));
        Assert.Equal("St. Mary's Fleet", SquadronFactionPhrase.Read("my squadron’s faction is St. Mary's Fleet."));
    }
}
