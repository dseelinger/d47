using D47.Core.Conversation;

namespace D47.App;

/// <summary>Decides which API key a language-model provider uses, and names where it came from.</summary>
internal static class LlmKeyResolution
{
    internal const string TestDriveVariable = "D47_COMPARE_KEY";

    /// <summary>
    /// A test drive's Anthropic key comes from <see cref="TestDriveVariable"/> first; otherwise the stored
    /// key, then ANTHROPIC_API_KEY for Anthropic only.
    /// </summary>
    internal static (string Key, string Source)? Resolve(
        bool isTestDrive,
        string providerId,
        string? storedKey,
        Func<string, string?> environment)
    {
        var isAnthropic = providerId == LlmProviderCatalog.AnthropicId;

        if (isTestDrive && isAnthropic && environment(TestDriveVariable) is { } dev && !string.IsNullOrWhiteSpace(dev))
        {
            return (dev, $"the {TestDriveVariable} environment variable (test drive)");
        }

        if (storedKey is not null)
        {
            return (storedKey, "the secret store");
        }

        if (!isAnthropic)
        {
            return null;
        }

        var fromEnvironment = environment("ANTHROPIC_API_KEY");
        return string.IsNullOrWhiteSpace(fromEnvironment)
            ? null
            : (fromEnvironment, "the ANTHROPIC_API_KEY environment variable");
    }
}
