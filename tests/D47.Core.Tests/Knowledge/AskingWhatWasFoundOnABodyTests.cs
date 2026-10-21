using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AskingWhatWasFoundOnABodyTests
{
    private const string System = "Synuefe XO-P c22-17";

    private sealed class FakeCommunity(params EarlierFind[] finds) : ICommunityBiology
    {
        public int Requests { get; private set; }

        public Task<IReadOnlyList<EarlierFind>> SystemFindsAsync(string systemName, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult<IReadOnlyList<EarlierFind>>(finds);
        }
    }

    private static EarlierFind Find(string body, string name) => new($"{System} {body}", 1, name, 10, 20);

    private static EarlierFind[] Finds() =>
    [
        Find("C 3 a", "Bacterium Aurasus - Green"),
        Find("C 3 a", "Bacterium Aurasus - Green"),
        Find("C 3 a", "Osseus Fractus - Indigo"),
        Find("C 3 a", "Stratum Tectonicas - Lime"),
        Find("C 3 a", "Stratum Tectonicas - Lime"),
        Find("C 3 a", "Stratum Tectonicas - Lime"),
        Find("D 1", "Fungoida Setisis - Emerald"),
    ];

    private static CapabilityRegistry Registry(
        FakeCommunity community,
        EarlierFinds store,
        bool on = true,
        string? body = null)
    {
        var gameState = new GameStateStore();

        foreach (var line in new[]
        {
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            $$"""{"timestamp":"2026-08-16T09:01:00Z","event":"Location","StarSystem":"{{System}}","SystemAddress":77,"Docked":false}""",
        }.Concat(body is null ? [] : new[]
        {
            $$"""{"timestamp":"2026-08-16T09:02:00Z","event":"ApproachBody","StarSystem":"{{System}}","SystemAddress":77,"Body":"{{body}}","BodyID":5}""",
        }))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        var settings = TestSurface.For(new MemoryInstall()).Settings;
        settings.Apply(GalaxyCapability.CommunityBiologyKey, on ? "true" : "false", SettingsCaller.Panel);

        return CapabilityRegistry.Build(
        [
            ExobiologyCapability.Create(
                null, () => gameState.Active, settings, community: community, earlier: store),
        ]);
    }

    private static async Task<string> Ask(CapabilityRegistry registry, string json = "{}") =>
        (await registry.InvokeAsync(
            ExobiologyCapability.EarlierFindsTool,
            ToolArguments.FromJson(json),
            TestContext.Current.CancellationToken)).Content;

    [Fact]
    public async Task ANamedBodyListsItsVariantsMostFirst()
    {
        var answer = await Ask(Registry(new FakeCommunity(Finds()), new EarlierFinds()), """{"body":"C 3 a"}""");

        Assert.Equal(
            "Canonn has 6 earlier finds on C 3 a: Stratum Tectonicas - Lime 3, Bacterium Aurasus - Green 2, "
            + "Osseus Fractus - Indigo 1.",
            answer);
    }

    [Fact]
    public async Task WithoutANameItUsesTheBodyTheCommanderIsAt()
    {
        var registry = Registry(new FakeCommunity(Finds()), new EarlierFinds(), body: $"{System} D 1");

        Assert.Equal("Canonn has 1 earlier find on D 1: Fungoida Setisis - Emerald 1.", await Ask(registry));
    }

    [Fact]
    public async Task ABodyWithNoFindsSaysSo()
    {
        var answer = await Ask(Registry(new FakeCommunity(Finds()), new EarlierFinds()), """{"body":"B 2"}""");

        Assert.Equal("Canonn has no earlier finds on B 2.", answer);
    }

    [Fact]
    public async Task WithTheSettingOffItRefusesAndAsksNothing()
    {
        var community = new FakeCommunity(Finds());
        var answer = await Ask(Registry(community, new EarlierFinds(), on: false), """{"body":"C 3 a"}""");

        Assert.Equal(
            "Earlier biology finds are off. Turn on Earlier biology finds from Canonn in Settings to ask.",
            answer);
        Assert.Equal(0, community.Requests);
    }

    [Fact]
    public async Task ASecondAskIsAnsweredFromTheStore()
    {
        var community = new FakeCommunity(Finds());
        var registry = Registry(community, new EarlierFinds());

        await Ask(registry, """{"body":"C 3 a"}""");
        await Ask(registry, """{"body":"D 1"}""");

        Assert.Equal(1, community.Requests);
    }

    [Theory]
    [InlineData("what has been found on this body")]
    [InlineData("what has been found here")]
    [InlineData("earlier finds on this body")]
    public void TheWordsReachTheToolWithoutTheModel(string phrase)
    {
        var capability = ExobiologyCapability.Create(null, () => null, TestSurface.For(new MemoryInstall()).Settings);

        Assert.Contains(capability.Keywords, keyword =>
            phrase.Contains(keyword.Phrase, StringComparison.OrdinalIgnoreCase)
            && keyword.ToolName == ExobiologyCapability.EarlierFindsTool);
    }
}
