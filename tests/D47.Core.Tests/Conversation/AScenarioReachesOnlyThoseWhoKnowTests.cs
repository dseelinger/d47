using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>The Commander's scenario goes into the prompt of a speaker its audience reaches, and no other.</summary>
public class AScenarioReachesOnlyThoseWhoKnowTests
{
    private const string Secret = "We are running unregistered weapons to the Brotherhood.";

    private const string Question = "tell me something about the long nights out here";

    private sealed class AddressedLine(VoiceRole role) : ILine
    {
        public bool IsOpen => false;

        public Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken) =>
            Task.FromResult<LineDecision>(new LineDecision.Taken(
                new Speaker(role, role.ToString(), $"You are the {role}.", [], OffersTools: false, Signal: 1),
                input));

        public void Close()
        {
        }
    }

    private static TurnLoop Build(TestSurface surface, ILlmProvider provider, ScenarioAudience audience, VoiceRole? addressed)
    {
        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = "You are the ship's AI.",
            Scenario = Secret,
            ScenarioAudience = audience,
        };

        if (addressed is { } role)
        {
            loop.Lines.Add(new AddressedLine(role));
        }

        return loop;
    }

    private static async Task<string> SystemBlockAsync(TurnLoop loop, FakeLlmProvider provider)
    {
        await foreach (var _ in loop.RunAsync(Question, cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        return provider.LastRequest!.Prompt.RenderCachedSystemBlock();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(VoiceRole.Crew)]
    public async Task AnAboardScenarioReachesTheShipsAiAndTheCrew(VoiceRole? addressed)
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Understood.");
        var loop = Build(TestSurface.For(install), provider, ScenarioAudience.Aboard, addressed);

        var block = await SystemBlockAsync(loop, provider);

        Assert.Contains(PromptAssembly.ScenarioLabel, block, StringComparison.Ordinal);
        Assert.Contains(Secret, block, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAboardScenarioDoesNotReachTheCarrierCaptain()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Captain here.");
        var loop = Build(TestSurface.For(install), provider, ScenarioAudience.Aboard, VoiceRole.CarrierCaptain);

        var block = await SystemBlockAsync(loop, provider);

        Assert.DoesNotContain(Secret, block, StringComparison.Ordinal);
        Assert.DoesNotContain(PromptAssembly.ScenarioLabel, block, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACarrierScenarioReachesTheCarrierCaptain()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Captain here.");
        var loop = Build(TestSurface.For(install), provider, ScenarioAudience.Carrier, VoiceRole.CarrierCaptain);

        Assert.Contains(Secret, await SystemBlockAsync(loop, provider), StringComparison.Ordinal);
    }

    [Fact]
    public void CommsHearsOnlyAPublicScenario()
    {
        Assert.False(ScenarioAudiences.Reaches(ScenarioAudience.Aboard, VoiceRole.Comms));
        Assert.False(ScenarioAudiences.Reaches(ScenarioAudience.Carrier, VoiceRole.Comms));
        Assert.True(ScenarioAudiences.Reaches(ScenarioAudience.Public, VoiceRole.Comms));
        Assert.False(ScenarioAudiences.Reaches(ScenarioAudience.Aboard, VoiceRole.TowerControl));
        Assert.True(ScenarioAudiences.Reaches(ScenarioAudience.Carrier, VoiceRole.TowerControl));
    }

    [Fact]
    public async Task ClearingTheScenarioRemovesItFromTheNextTurn()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Understood.");
        var loop = Build(TestSurface.For(install), provider, ScenarioAudience.Aboard, addressed: null);

        Assert.Contains(Secret, await SystemBlockAsync(loop, provider), StringComparison.Ordinal);

        loop.Scenario = null;

        Assert.DoesNotContain(PromptAssembly.ScenarioLabel, await SystemBlockAsync(loop, provider), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePrivacySectionNamesTheScenarioOnlyWhenOneIsSet()
    {
        var empty = new D47Settings();
        var set = empty with { Llm = empty.Llm with { Scenario = Secret } };

        var without = EgressDisclosure.Entry(EgressDisclosure.LanguageModel, empty, llmKeyPresent: true);
        var with = EgressDisclosure.Entry(EgressDisclosure.LanguageModel, set, llmKeyPresent: true);

        Assert.DoesNotContain("scenario", without.What, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scenario", without.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("your current scenario", with.What, StringComparison.Ordinal);
        Assert.Contains("your current scenario", with.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePrivacySectionNamesTheSheetAndAboutMeWhenTheyAreSet()
    {
        var empty = new D47Settings();
        var set = empty with { Llm = empty.Llm with { CharacterSheet = "Cmdr Vale", AboutMe = "A long story." } };

        var what = EgressDisclosure.Entry(EgressDisclosure.LanguageModel, set, llmKeyPresent: true).What;

        Assert.Contains("your character sheet and your backstory", what, StringComparison.Ordinal);
    }

    [Fact]
    public void OneCommandersScenarioIsTheirsAndSurvivesARestart()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        surface.Settings.UseCommander("F1", "Alice");
        surface.Settings.Apply("llm.scenario", Secret, SettingsCaller.Panel);
        surface.Settings.Apply("llm.scenarioAudience", "carrier", SettingsCaller.Panel);

        var reloaded = TestSurface.For(install);
        Assert.Null(reloaded.Settings.Current.Llm.Scenario);

        reloaded.Settings.UseCommander("F2", "Bob");
        Assert.Null(reloaded.Settings.Current.Llm.Scenario);
        Assert.Equal(ScenarioAudience.Aboard, reloaded.Settings.Current.Llm.ScenarioAudience);

        reloaded.Settings.UseCommander("F1", "Alice");
        Assert.Equal(Secret, reloaded.Settings.Current.Llm.Scenario);
        Assert.Equal(ScenarioAudience.Carrier, reloaded.Settings.Current.Llm.ScenarioAudience);
    }
}
