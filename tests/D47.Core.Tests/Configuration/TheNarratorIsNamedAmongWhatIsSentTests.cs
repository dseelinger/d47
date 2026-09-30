using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The privacy and egress section says when the Narrator sends the Commander's texts.</summary>
public class TheNarratorIsNamedAmongWhatIsSentTests
{
    private static D47Settings WithBackstory(bool narrator = true, string? endpoint = null)
    {
        var settings = new D47Settings();

        return settings with
        {
            Llm = settings.Llm with { AboutMe = "A long story.", Endpoint = endpoint },
            Callouts = settings.Callouts with { Narrator = narrator },
        };
    }

    private static string What(D47Settings settings) =>
        EgressDisclosure.Entry(EgressDisclosure.LanguageModel, settings, llmKeyPresent: true).What;

    [Fact]
    public void AHostedModelIsSaidToReceiveNarrations() =>
        Assert.Contains("The Narrator sends your backstory each time it narrates.", What(WithBackstory()), StringComparison.Ordinal);

    [Fact]
    public void ALoopbackModelIsSaidToReceiveNarrations() =>
        Assert.Contains(
            "The Narrator sends your backstory to that address each time it narrates.",
            What(WithBackstory(endpoint: "http://localhost:11434")),
            StringComparison.Ordinal);

    [Fact]
    public void WithTheNarratorOffItIsNotNamed() =>
        Assert.DoesNotContain("Narrator", What(WithBackstory(narrator: false)), StringComparison.Ordinal);

    [Fact]
    public void WithNothingSetTheNarratorIsNotNamed() =>
        Assert.DoesNotContain("Narrator", What(new D47Settings()), StringComparison.Ordinal);
}
