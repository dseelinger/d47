using Xunit;

namespace D47.App.Tests;

/// <summary>Which calls take the cheap model and which keep the Commander's.</summary>
public class TheFloorReachesTheBackgroundCallsAndOnlyThemTests
{
    /// <summary>The readers of the conversation model, exactly.</summary>
    private static readonly string[] KeepTheConversationModel =
    [
        // The Commander's log, quoted at a price before anything is written.
        "Model = self?.Turns.Model,",

        // Adventure generation.
        "() => turns.Model,",

        // Advice on a ship's build, which the Commander waits on (#570).
        "() => self?.Turns.Model,",

        // The debrief's proposed wording.
        "Turns.Model,",

        // Flagged, not fixed: correct today because web search is endpoint-gated in all three providers, and
        // the contract says it is model-gated in principle.
        "|| provider.CapabilitiesFor(Turns.Model ?? provider.DefaultModel).SupportsWebSearch;",

        // The model row's line about a small context, for the model the turns are sent to (#423).
        "|| provider.CapabilitiesFor(Turns.Model ?? provider.DefaultModel).ContextTokens is not { } context)",

        // Whether the model the turns are sent to reads a picture of the screen, and the row's line when not.
        "|| provider.CapabilitiesFor(Turns.Model ?? provider.DefaultModel).SupportsImages;",
        "? ConversationCapability.PictureNote(Turns.Model ?? provider.DefaultModel)",
    ];

    [Fact]
    public void TheConversationModelIsReadByExactlyTheCallsThatShouldReadIt()
    {
        var readers = CodeLinesContaining("Turns.Model", "turns.Model")
            .Where(line => !line.StartsWith("Turns.Model =", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(KeepTheConversationModel.Order(), readers.Order());
    }

    /// <summary>
    /// Twelve callers, all of them carrying no conversation history and already declaring a cold prefix —
    /// which is what makes pointing them at a cheap model cost no cache at all.
    /// </summary>
    [Fact]
    public void TheBackgroundModelIsReadByTheTwelveCallsTheCommanderIsNotWaitingOn()
    {
        var readers = CodeLinesContaining("Turns.BackgroundModel")
            .Where(line => !line.StartsWith("Turns.BackgroundModel =", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(12,readers.Count);
        Assert.All(readers, line => Assert.Equal("Turns.BackgroundModel,", line));
    }

    /// <summary>
    /// Resolved once, where settings are applied, so null means the two are the same model and every
    /// one of the nine behaves exactly as it did.
    /// </summary>
    [Fact]
    public void TheBackgroundModelIsResolvedInOnePlace()
    {
        Assert.Contains(
            "Turns.BackgroundModel = BackgroundModels.Resolve(current);",
            CodeLinesContaining("Turns.BackgroundModel ="));
    }

    /// <summary>Every code line in the tree mentioning any of <paramref name="fragments"/>.</summary>
    private static List<string> CodeLinesContaining(params string[] fragments) =>
        [.. AppSource.CodeLines(fragments).Select(line => line.Text)];
}
