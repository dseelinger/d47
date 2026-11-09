using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Campaigns;

/// <summary>The campaign phrases reach the protected tools without the model, and only for systems the campaign faction is known in.</summary>
public sealed class SystemIntentsAreSetByVoiceTests
{
    private const string Us = "Kestrel Reach Compact";

    private readonly CampaignStore _store = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private readonly FactionSystemsSeen _seen = new();

    public SystemIntentsAreSetByVoiceTests()
    {
        _store.SetFaction("F1", Us, clearIntents: false);
        Jump("Lave", Us, "Lave Purple Gang");
        Jump("Diso", "Diso Gold Cartel");
    }

    private void Jump(string system, params string[] factions)
    {
        var listed = string.Join(",", factions.Select(name => $$"""{ "Name":"{{name}}", "Influence":0.3 }"""));
        var line = $$"""{ "timestamp":"2026-11-01T12:00:00Z", "event":"FSDJump", "StarSystem":"{{system}}", "SystemAddress":1, "StarPos":[0,0,0], "Factions":[{{listed}}] }""";

        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        _state.Apply(parsed!);
    }

    private KeywordRouter Router(IGalaxyService? galaxy = null) =>
        new(
            CapabilityRegistry.Build([CampaignCapability.Create(_store, () => _state, galaxy: galaxy, seen: _seen)]),
            () => CampaignCapability.Phrases(_store, () => _state, seen: _seen));

    [Theory]
    [InlineData("hold Lave", "Hold")]
    [InlineData("grow lave", "Grow")]
    [InlineData("expand from Lave", "ExpandFromHere")]
    [InlineData("keep Lave under seventy-five", "StayUnder75")]
    [InlineData("keep Lave under 75%", "StayUnder75")]
    [InlineData("keep Lave under seventy five percent", "StayUnder75")]
    [InlineData("take control of Lave", "TakeControl")]
    [InlineData("leave Lave alone", "LeaveAlone")]
    public void EachIntentHasAPhrase(string said, string intent)
    {
        var match = Router().MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal(CampaignCapability.SetIntentTool, match.ToolName);
        Assert.True(match.Arguments.TryGetString("system", out var system));
        Assert.Equal("Lave", system);
        Assert.True(match.Arguments.TryGetString("intent", out var heard));
        Assert.Equal(intent, heard);
    }

    [Theory]
    [InlineData("hold Diso")]
    [InlineData("hold on a second")]
    [InlineData("leave me alone")]
    public void APhraseNamingNoSystemOfTheFactionIsNotTaken(string said) =>
        Assert.Null(Router().MatchToolCommand(said));

    [Fact]
    public void ASystemWithAnIntentKeepsItsPhrases()
    {
        _store.SetIntent("F1", "Riedquat", SystemIntent.Grow);

        Assert.Equal(CampaignCapability.SetIntentTool, Router().MatchToolCommand("hold Riedquat")?.ToolName);
    }

    [Fact]
    public async Task AFetchedReportAddsTheFactionsOtherSystems()
    {
        var galaxy = new Galaxy(new FactionReport(Us, null, null, [new FactionSystem { Name = "Zaonce", Controls = true }], []));
        var router = Router(galaxy);

        Assert.Null(router.MatchToolCommand("hold Zaonce"));

        await Invoke(galaxy, CampaignCapability.GetTool, []);

        Assert.Equal(CampaignCapability.SetIntentTool, router.MatchToolCommand("hold Zaonce")?.ToolName);
    }

    [Fact]
    public async Task TheIntentIsStoredAndConfirmed()
    {
        var result = await Invoke(null, CampaignCapability.SetIntentTool, new() { ["system"] = "Lave", ["intent"] = "StayUnder75" });

        Assert.Equal("Lave: keep under 75%.", result.Content);
        Assert.Equal(SystemIntent.StayUnder75, _store.For("F1").Intents["Lave"]);
    }

    [Fact]
    public async Task AnIntentOffTheListIsRefused()
    {
        var result = await Invoke(null, CampaignCapability.SetIntentTool, new() { ["system"] = "Lave", ["intent"] = "Conquer" });

        Assert.True(result.IsError);
        Assert.Empty(_store.For("F1").Intents);
    }

    [Fact]
    public void TheSetToolsAreProtectedAndTheReadIsNot()
    {
        var tools = CampaignCapability.Create(_store, () => _state).Tools.ToDictionary(tool => tool.Name);

        Assert.True(tools[CampaignCapability.SetFactionTool].Protected);
        Assert.True(tools[CampaignCapability.SetIntentTool].Protected);
        Assert.False(tools[CampaignCapability.GetTool].Protected);
    }

    [Theory]
    [InlineData("our campaign faction is Hutton Truckers", "Hutton Truckers")]
    [InlineData("My campaign faction is called Mother Gaia.", "Mother Gaia")]
    [InlineData("the campaign faction is now Lave Purple Gang", "Lave Purple Gang")]
    [InlineData("our squadron's faction is Hutton Truckers", null)]
    [InlineData("our campaign faction is", null)]
    public void TheCampaignFactionIsHeardWithoutAModel(string said, string? faction) =>
        Assert.Equal(faction, CampaignFactionPhrase.Read(said));

    [Fact]
    public async Task NamingADifferentFactionClearsTheIntents()
    {
        _store.SetIntent("F1", "Lave", SystemIntent.Grow);
        var galaxy = new Galaxy(new FactionReport("Hutton Truckers", null, null, [new FactionSystem { Name = "Hutton" }], []));

        var result = await Invoke(galaxy, CampaignCapability.SetFactionTool, new() { ["faction"] = "hutton truckers" });

        Assert.Equal($"The campaign faction is Hutton Truckers. The intents set for {Us}'s systems are cleared.", result.Content);
        Assert.Equal("Hutton Truckers", _store.For("F1").Faction);
        Assert.Empty(_store.For("F1").Intents);
    }

    [Fact]
    public async Task IntentsSetWithNoFactionKnownAreClearedWithoutNamingOne()
    {
        var store = new CampaignStore("other.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);
        store.SetIntent("F1", "Lave", SystemIntent.Grow);
        var galaxy = new Galaxy(new FactionReport("Hutton Truckers", null, null, [new FactionSystem { Name = "Hutton" }], []));

        var result = await CampaignCapability.Create(store, () => _state, galaxy: galaxy).Tools
            .Single(definition => definition.Name == CampaignCapability.SetFactionTool)
            .Handler(new ToolArguments(new Dictionary<string, string> { ["faction"] = "Hutton Truckers" }), TestContext.Current.CancellationToken);

        Assert.Equal("The campaign faction is Hutton Truckers. The intents set before are cleared.", result.Content);
        Assert.Empty(store.For("F1").Intents);
    }

    [Fact]
    public async Task AFactionTheGalaxyDoesNotKnowIsNotStored()
    {
        var galaxy = new Galaxy(new FactionReport("Nobody", null, null, [], []));

        var result = await Invoke(galaxy, CampaignCapability.SetFactionTool, new() { ["faction"] = "Nobody" });

        Assert.True(result.IsError);
        Assert.Equal(Us, _store.For("F1").Faction);
    }

    [Fact]
    public async Task TheCampaignListsEachSystemWithItsIntent()
    {
        _store.SetIntent("F1", "Lave", SystemIntent.Grow);
        var galaxy = new Galaxy(new FactionReport(
            Us,
            null,
            null,
            [new FactionSystem { Name = "Lave" }, new FactionSystem { Name = "Zaonce", Controls = true }, new FactionSystem { Name = "Tionisla" }],
            []));

        var result = await Invoke(galaxy, CampaignCapability.GetTool, []);

        Assert.Equal(
            $"The campaign faction is {Us}. Lave: grow. Tionisla: leave alone (default). Zaonce: hold (default). "
            + "The chronicle holds no resolved fronts yet.",
            result.Content);
        Assert.Equal($"The campaign faction is {Us}. 3 systems: 1 to hold, 1 to grow, 1 to leave alone.", result.Spoken);
    }

    [Fact]
    public async Task WithoutGalaxySearchOnlyTheSetIntentsAreListed()
    {
        _store.SetIntent("F1", "Lave", SystemIntent.TakeControl);

        var result = await Invoke(new Galaxy(null), CampaignCapability.GetTool, []);

        Assert.Equal(
            $"The campaign faction is {Us}. The galaxy service is not answering, so only the systems with an intent set "
            + "are listed. Lave: take control. The chronicle holds no resolved fronts yet.",
            result.Content);
        Assert.Equal(
            $"The campaign faction is {Us}. The galaxy service is not answering, so only the systems with an intent set "
            + "count: 1 to take control.",
            result.Spoken);
    }

    private async Task<ToolResult> Invoke(IGalaxyService? galaxy, string tool, Dictionary<string, string> arguments) =>
        await CampaignCapability.Create(_store, () => _state, galaxy: galaxy, seen: _seen).Tools
            .Single(definition => definition.Name == tool)
            .Handler(new ToolArguments(arguments), TestContext.Current.CancellationToken);

    private sealed class Galaxy(FactionReport? report) : StubGalaxyService
    {
        public override Task<FactionReport> FactionAsync(
            string faction,
            CancellationToken cancellationToken,
            bool refresh = false,
            IProgress<FactionProgress>? progress = null) =>
            report is null
                ? throw new GalaxyUnavailableException("The galaxy service is not answering.")
                : Task.FromResult(report);
    }
}
