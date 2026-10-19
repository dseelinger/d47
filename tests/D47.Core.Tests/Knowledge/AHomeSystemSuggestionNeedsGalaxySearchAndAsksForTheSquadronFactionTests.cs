using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>suggest_home_system sends nothing with galaxy search off and says when the squadron case was skipped (#1313).</summary>
public class AHomeSystemSuggestionNeedsGalaxySearchAndAsksForTheSquadronFactionTests
{
    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private readonly SettingsService _settings = TestSurface.For(new MemoryInstall()).Settings;

    private readonly EmptyGalaxy _galaxy = new();

    private sealed class EmptyGalaxy : IGalaxyService
    {
        public int Calls { get; private set; }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new GalaxySearchResult(null, 0, []));
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new BodySearchResult(null, 0, []));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private Task<ToolResult> SuggestAsync() =>
        CapabilityRegistry.Build([GalaxyCapability.Create(_galaxy, () => "Sol", _settings, gameState: () => _state)])
            .InvokeAsync(
                GalaxyCapability.SuggestHomeSystemTool,
                new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)),
                TestContext.Current.CancellationToken,
                ToolCaller.Model);

    [Fact]
    public async Task WithGalaxySearchOffTheToolSaysSoAndSendsNothing()
    {
        var result = await SuggestAsync();

        Assert.True(result.IsError);
        Assert.Contains("needs galaxy search", result.Content, StringComparison.Ordinal);
        Assert.Equal(0, _galaxy.Calls);
    }

    [Fact]
    public async Task ASquadronMemberWithNoStoredFactionIsToldTheSquadronCaseWasSkipped()
    {
        _settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
        Assert.True(JournalEvent.TryParse(
            "{ \"timestamp\":\"2026-10-19T12:00:00Z\", \"event\":\"SquadronStartup\", \"SquadronName\":\"GREYBEARD DELTA\", \"SquadronID\":1234 }",
            NullLogger.Instance,
            out var startup));
        _state.Apply(startup!);

        var result = await SuggestAsync();

        Assert.False(result.IsError);
        Assert.Contains("Case: interests", result.Content, StringComparison.Ordinal);
        Assert.Contains("squadron case was skipped", result.Content, StringComparison.Ordinal);
    }
}
