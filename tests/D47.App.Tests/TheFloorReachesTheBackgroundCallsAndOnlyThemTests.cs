using Xunit;

namespace D47.App.Tests;

/// <summary>Which calls take the cheap model and which keep the Commander's.</summary>
[Trait("Category", "Gate")]
public class TheFloorReachesTheBackgroundCallsAndOnlyThemTests
{
    /// <summary>The readers of the conversation model, exactly.</summary>
    private static readonly string[] KeepTheConversationModel =
    [
        // The Commander's log, quoted at a price before anything is written.
        "Model = late.Host?.Turns.Model,",

        // Adventure generation.
        "() => turns.Model,",

        // The debrief's proposed wording.
        "Turns.Model,",

        // The Narrator's recap, which the Commander asked for and waits on.
        "Turns.Model,",

        // Whether a journal condition would tick a line typed into the checklist's Add box (#1058).
        "var ask = TypedLineWatch.Asker(provider, Turns.Model, Spend, PriceTable.Default);",

        // Flagged, not fixed: correct today because web search is endpoint-gated in all three providers, and
        // the contract says it is model-gated in principle.
        "|| provider.CapabilitiesFor(Turns.Model ?? provider.DefaultModel).SupportsWebSearch;",

        // The model row's line about a small context, for the model the turns are sent to (#423).
        "|| provider.CapabilitiesFor(Turns.Model ?? provider.DefaultModel).ContextTokens is not { } context)",

        // Whether the model the turns are sent to reads a picture of the screen, and the row's line when not.
        "|| provider.CapabilitiesFor(Turns.Model ?? provider.DefaultModel).SupportsImages;",
        "? ConversationCapability.PictureNote(Turns.Model ?? provider.DefaultModel)",
    ];

    /// <summary>Advice on a ship's build runs on the high model, not the conversation model.</summary>
    [Fact]
    public void TheBuildAdvisorReadsTheHighModel()
    {
        var readers = CodeLinesContaining("Turns.HighModel")
            .Where(line => !line.StartsWith("Turns.HighModel =", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(["() => late.Host?.Turns.HighModel,"], readers);
    }

    [Fact]
    public void TheConversationModelIsReadByExactlyTheCallsThatShouldReadIt()
    {
        var readers = CodeLinesContaining("Turns.Model", "turns.Model")
            .Where(line => !line.StartsWith("Turns.Model =", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(KeepTheConversationModel.Order(), readers.Order());
    }

    /// <summary>
    /// Thirteen callers, all of them carrying no conversation history and already declaring a cold prefix —
    /// which is what makes pointing them at a cheap model cost no cache at all.
    /// </summary>
    [Fact]
    public void TheBackgroundModelIsReadByTheThirteenCallsTheCommanderIsNotWaitingOn()
    {
        var readers = CodeLinesContaining("Turns.BackgroundModel")
            .Where(line => !line.StartsWith("Turns.BackgroundModel =", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(13, readers.Count);
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
