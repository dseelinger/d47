using D47.Core.Conversation;
using Xunit;

namespace D47.App.Tests;

public class ATestDriveUsesTheDevKeyWhenOneIsSetTests
{
    private static Func<string, string?> Environment(string? compare = null, string? anthropic = null) =>
        name => name switch
        {
            "D47_COMPARE_KEY" => compare,
            "ANTHROPIC_API_KEY" => anthropic,
            _ => null,
        };

    [Fact]
    public void ATestDriveTakesTheDevKeyAheadOfTheSecretStore()
    {
        var resolved = LlmKeyResolution.Resolve(true, LlmProviderCatalog.AnthropicId, "stored", Environment("dev"));

        Assert.Equal(("dev", "the D47_COMPARE_KEY environment variable (test drive)"), resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ATestDriveWithoutTheDevKeyResolvesAsBefore(string? compare)
    {
        Assert.Equal(
            ("stored", "the secret store"),
            LlmKeyResolution.Resolve(true, LlmProviderCatalog.AnthropicId, "stored", Environment(compare, "env")));
        Assert.Equal(
            ("env", "the ANTHROPIC_API_KEY environment variable"),
            LlmKeyResolution.Resolve(true, LlmProviderCatalog.AnthropicId, null, Environment(compare, "env")));
    }

    [Fact]
    public void AReleaseBuildNeverReadsTheDevKey()
    {
        var asked = new List<string>();
        var resolved = LlmKeyResolution.Resolve(
            false,
            LlmProviderCatalog.AnthropicId,
            "stored",
            name =>
            {
                asked.Add(name);
                return "dev";
            });

        Assert.Equal(("stored", "the secret store"), resolved);
        Assert.DoesNotContain("D47_COMPARE_KEY", asked);
    }

    [Fact]
    public void AnotherProviderIgnoresTheDevKeyInATestDrive()
    {
        Assert.Equal(
            ("stored", "the secret store"),
            LlmKeyResolution.Resolve(true, LlmProviderCatalog.OpenAiId, "stored", Environment("dev")));
        Assert.Null(LlmKeyResolution.Resolve(true, LlmProviderCatalog.OpenAiId, null, Environment("dev", "env")));
    }
}
