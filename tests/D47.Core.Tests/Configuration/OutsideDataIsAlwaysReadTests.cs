using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using D47.Core.Tests.Knowledge;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>#2109: reading outside data has no setting, on new and existing installs alike.</summary>
public class OutsideDataIsAlwaysReadTests
{
    private static readonly string[] RemovedProperties =
    [
        "GalaxySearch", "NotablePlaces", "CommunityBiology", "BgsTick", "GalNet", "ServerStatus", "SignalIndex",
        "ColonisationPlanner",
    ];

    private static readonly string[] RemovedKeys =
    [
        "knowledge.galaxy", "knowledge.notablePlaces", "knowledge.communityBiology", "knowledge.bgsTick",
        "knowledge.galNet", "knowledge.serverStatus", "knowledge.signalIndex", "knowledge.colonisationPlanner",
    ];

    private static readonly string[] Reads =
    [
        EgressDisclosure.GalaxySearch, EgressDisclosure.NotablePlaces, EgressDisclosure.CommunityBiology,
        EgressDisclosure.BgsTick, EgressDisclosure.GalNet, EgressDisclosure.ServerStatus, EgressDisclosure.SignalIndex,
        EgressDisclosure.ColonisationPlanner,
    ];

    private sealed class Galaxy : StubGalaxyService
    {
        public GalaxyQuery? Asked { get; private set; }

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Asked = query;
            return Task.FromResult(new GalaxySearchResult("Sol", 1, [new SystemSummary { Name = "Alpha Centauri", Distance = 4.38 }]));
        }
    }

    [Fact]
    public void KnowledgeSettingsHoldsNoneOfTheReadSwitches()
    {
        var names = typeof(KnowledgeSettings).GetProperties().Select(property => property.Name);

        Assert.Empty(names.Intersect(RemovedProperties));
    }

    [Fact]
    public void NoSettingRowHasTheirKeys()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        var keys = settings.Sections.SelectMany(section => section.Rows).Select(row => row.Key);

        Assert.Empty(keys.Intersect(RemovedKeys));
    }

    [Fact]
    public void EveryReadIsActiveOnADefaultInstall()
    {
        var entries = EgressDisclosure.For(new D47Settings(), llmKeyPresent: false);

        Assert.All(Reads, id => Assert.True(entries.Single(entry => entry.Id == id).Active, id));
    }

    [Fact]
    public async Task AFileThatTurnedGalaxySearchOffIsStillAnsweredFromTheGalaxyService()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SettingsFile, """{ "knowledge": { "galaxySearch": false } }""");
        var galaxy = new Galaxy();
        var registry = CapabilityRegistry.Build(
            [GalaxyCapability.Create(galaxy, () => "Sol", TestSurface.For(install).Settings)]);

        var result = await registry.InvokeAsync(
            "search_systems",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["distance"] = "20" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);
        Assert.NotNull(galaxy.Asked);
        Assert.Contains("Alpha Centauri", result.Content, StringComparison.Ordinal);
    }
}
