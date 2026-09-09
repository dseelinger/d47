namespace D47.Core.Conversation;

/// <summary>Chooses reasoning effort per turn (Phase 3, "Model Level and Thinking").</summary>
public static class EffortRouter
{
    /// <summary>Words that mean the Commander wants the model to work at it.</summary>
    private static readonly string[] DeliberateSignals =
    [
        "carefully", "think hard", "think about", "work out", "figure out",
        "explain why", "walk me through", "in detail", "step by step",
    ];

    /// <summary>Work that is inherently multi-constraint: planning, comparing, optimising.</summary>
    private static readonly string[] ReasoningSignals =
    [
        "plan", "route", "compare", "cheapest", "fastest", "best", "optimal", "optimise",
        "optimize", "should i", "worth it", "trade", "profit", "engineer", "build",
        "loadout", "why", "how many", "how much", "how far",
    ];

    /// <summary>Lookups: the answer is a fact, not a judgement.</summary>
    private static readonly string[] LookupSignals =
    [
        "what is", "what's", "where is", "where's", "where am i", "who is", "when",
        "status", "am i", "do i have", "list",
    ];

    public static ThinkingEffort ChooseFor(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ThinkingEffort.Low;
        }

        var text = input.Trim().ToLowerInvariant();

        // An explicit ask to deliberate outranks everything else — the Commander said so.
        if (DeliberateSignals.Any(signal => text.Contains(signal, StringComparison.Ordinal)))
        {
            return ThinkingEffort.Max;
        }

        var reasoning = ReasoningSignals.Count(signal => text.Contains(signal, StringComparison.Ordinal));
        if (reasoning >= 2)
        {
            return ThinkingEffort.High;
        }

        var isLookup = LookupSignals.Any(signal => text.Contains(signal, StringComparison.Ordinal));

        if (reasoning == 1)
        {
            // One reasoning word inside an otherwise plain lookup ("what's the best route") still deserves
            // more than the floor, but not the ceiling.
            return isLookup ? ThinkingEffort.Medium : ThinkingEffort.High;
        }

        // Short and lookup-shaped: the cheapest setting answers it as well as any other.
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return isLookup && words <= 8 ? ThinkingEffort.Low : ThinkingEffort.Medium;
    }
}
