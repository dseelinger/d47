using Xunit;

namespace D47.App.Tests;

/// <summary>Which calls take the cheap model and which keep the Commander's.</summary>
public class TheFloorReachesTheBackgroundCallsAndOnlyThemTests
{
    /// <summary>The three readers of the conversation model, exactly.</summary>
    private static readonly string[] KeepTheConversationModel =
    [
        // The Commander's log, quoted at a price before anything is written.
        "Model = self?.Turns.Model,",

        // Adventure generation.
        "() => turns.Model,",

        // Flagged, not fixed: correct today because web search is endpoint-gated in all three providers, and
        // the contract says it is model-gated in principle.
        "|| provider.CapabilitiesFor(Turns.Model ?? provider.DefaultModel).SupportsWebSearch;",
    ];

    [Fact]
    public void TheConversationModelIsReadByExactlyTheThreeCallsThatShouldReadIt()
    {
        var readers = CodeLinesContaining("Turns.Model", "turns.Model")
            .Where(line => !line.StartsWith("Turns.Model =", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(KeepTheConversationModel.Order(), readers.Order());
    }

    /// <summary>
    /// Ten callers, all of them carrying no conversation history and already declaring a cold prefix —
    /// which is what makes pointing them at a cheap model cost no cache at all.
    /// </summary>
    [Fact]
    public void TheBackgroundModelIsReadByTheTenCallsTheCommanderIsNotWaitingOn()
    {
        var readers = CodeLinesContaining("Turns.BackgroundModel")
            .Where(line => !line.StartsWith("Turns.BackgroundModel =", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(10, readers.Count);
        Assert.All(readers, line => Assert.Equal("Turns.BackgroundModel,", line));
    }

    /// <summary>
    /// Resolved once, where settings are applied, so null means the two are the same model and every
    /// one of the nine behaves exactly as it did.
    /// </summary>
    [Fact]
    public void TheBackgroundModelFallsBackToTheConversationModelInOnePlace()
    {
        Assert.Contains(
            "Turns.BackgroundModel = current.Llm.BackgroundModel ?? current.Llm.Model;",
            CodeLinesContaining("Turns.BackgroundModel ="));
    }

    /// <summary>
    /// Every line of <c>AppHost.cs</c> mentioning any of <paramref name="fragments"/>, trimmed, with
    /// comments left out — the comments discuss both properties by name at length, and a gate that
    /// counted those would be counting its own explanation.
    /// </summary>
    private static List<string> CodeLinesContaining(params string[] fragments) =>
        [.. File.ReadAllLines(Path.Combine(RepositoryRoot(), "src", "D47.App", "AppHost.cs"))
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .Where(line => fragments.Any(fragment => line.Contains(fragment, StringComparison.Ordinal)))];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
