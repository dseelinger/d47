using D47.Core.Configuration;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The language model's entry names a hidden story among what is sent, and quotes none of it.</summary>
public class TheEgressListNamesAHiddenStoryTests
{
    private static string What(string? endpoint = null)
    {
        var settings = new D47Settings();

        return EgressDisclosure.Entry(
            EgressDisclosure.LanguageModel,
            settings with { Llm = settings.Llm with { Endpoint = endpoint } },
            llmKeyPresent: true).What;
    }

    [Fact]
    public void AHostedModelIsSaidToReceiveAHiddenStory() =>
        Assert.Contains("a hidden story, sent to the language model", What(), StringComparison.Ordinal);

    [Fact]
    public void ALoopbackModelIsSaidToReceiveAHiddenStory() =>
        Assert.Contains("a hidden story while a stock story runs", What("http://localhost:11434"), StringComparison.Ordinal);

    [Fact]
    public void NoHiddenTextIsQuoted()
    {
        foreach (var what in new[] { What(), What("http://localhost:11434") })
        {
            foreach (var secret in StoryFixtures.Catalog.Secrets)
            {
                foreach (var (_, field) in secret.Texts().Where(text => text.Text.Length > 0))
                {
                    Assert.True(!what.Contains(field, StringComparison.OrdinalIgnoreCase), $"The entry quotes hidden text from {secret.Id}.");
                }
            }
        }
    }
}
