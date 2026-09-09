using D47.Core.Conversation;

namespace D47.Core.Lore;

/// <summary>
/// The second half of an arrival remark: what a web search turns up about the place, and the rules for
/// saying it (Phase 23, "Look it up, and say where the answer came from").
/// </summary>
public static class LoreLookup
{
    /// <summary>How long the follow-up may take before it is abandoned.</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Cold (#98), and here beside the instruction rather than at the call site for the reason the rest
    /// of this class is here: the property that matters is assertable against a value instead of
    /// against a running app.
    /// </summary>
    public static readonly LlmSampling Sampling = LlmSampling.Lore;

    /// <summary>What the model is asked.</summary>
    public static string Instruction(string systemName) =>
        $"Search the web for what is notable about the Elite Dangerous system \"{systemName}\" — "
        + "its history, what has been found there, or why Commanders go. Reply with one or two "
        + "sentences of what you actually found, and nothing else. If the search turns up nothing "
        + "specific to that system, reply with exactly: NOTHING. Do not guess, do not describe its "
        + "stars or planets, and do not mention searching.";

    /// <summary>
    /// The exact word a model is told to answer with when it found nothing, so "no result" is a value
    /// rather than a sentence somebody has to recognise.
    /// </summary>
    public const string NothingFound = "NOTHING";

    /// <summary>The line to speak, or null when there is nothing worth saying.</summary>
    public static string? Spoken(string? found)
    {
        var line = found?.Trim();

        if (string.IsNullOrEmpty(line)
            || line.TrimEnd('.').Equals(NothingFound, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"From a search: {line}";
    }

    /// <summary>
    /// What is said instead when the Commander asked for a lookup and the endpoint cannot run one —
    /// appended to the bare fact rather than replacing it.
    /// </summary>
    public const string CannotSearch = "I cannot search from here, so that is all I have on it.";

    /// <summary>Whether a result that has just come back is still worth speaking.</summary>
    public static bool StillHere(long remarkedOn, long? whereTheyAreNow) => whereTheyAreNow == remarkedOn;
}
