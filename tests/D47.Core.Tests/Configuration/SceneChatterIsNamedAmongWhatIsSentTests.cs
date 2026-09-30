using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The privacy and egress section says scene chatter sends the scenario at every audience.</summary>
public class SceneChatterIsNamedAmongWhatIsSentTests
{
    private static D47Settings WithScenario(bool scenes = true, string? endpoint = null, string? scenario = "A secret raid.")
    {
        var settings = new D47Settings();

        return settings with
        {
            Llm = settings.Llm with { Scenario = scenario, ScenarioAudience = ScenarioAudience.Aboard, Endpoint = endpoint },
            Callouts = settings.Callouts with { Scenes = scenes },
        };
    }

    private static string What(D47Settings settings) =>
        EgressDisclosure.Entry(EgressDisclosure.LanguageModel, settings, llmKeyPresent: true).What;

    [Fact]
    public void AHostedModelIsSaidToReceiveTheScenarioFromScenes() =>
        Assert.Contains(
            "Scene chatter at a settlement, in a ship fight or over a mission sends your current scenario, whatever Who knows about it is set to.",
            What(WithScenario()),
            StringComparison.Ordinal);

    [Fact]
    public void ALoopbackModelIsSaidToReceiveTheScenarioFromScenes() =>
        Assert.Contains(
            "Scene chatter at a settlement, in a ship fight or over a mission sends your current scenario to that address, whatever Who knows about it is set to.",
            What(WithScenario(endpoint: "http://localhost:11434")),
            StringComparison.Ordinal);

    [Fact]
    public void WithScenesOffItIsNotNamed() =>
        Assert.DoesNotContain("Scene chatter", What(WithScenario(scenes: false)), StringComparison.Ordinal);

    [Fact]
    public void WithNoScenarioItIsNotNamed() =>
        Assert.DoesNotContain("Scene chatter", What(WithScenario(scenario: null)), StringComparison.Ordinal);
}
