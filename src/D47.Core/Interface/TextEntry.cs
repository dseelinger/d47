namespace D47.Core.Interface;

/// <summary>Which way of entering text opens first (Phase 25, "Say it, or type it").</summary>
public enum EntrySurface
{
    /// <summary>d47 listens, shows what it hears as it hears it, and reads back what it got.</summary>
    Voice,

    /// <summary>The drawn keyboard, straight away.</summary>
    Keyboard,
}

/// <summary>Why the keyboard came back on its own (Phase 25).</summary>
public enum EntryFallback
{
    /// <summary>Nothing came back at all.</summary>
    NothingHeard,

    /// <summary>Something came back and the transcriber was not sure of it.</summary>
    LowConfidence,

    /// <summary>A confident transcription of a value that is not a thing.</summary>
    DidNotResolve,
}

/// <summary>What a caller made of a value it was offered.</summary>
/// <param name="Accepted">Whether it is a thing.</param>
/// <param name="Complaint">
/// What was wrong with it, in the Commander's terms, for the keyboard to open under.
/// </param>
public sealed record EntryVerdict(bool Accepted, string? Complaint = null)
{
    public static readonly EntryVerdict Ok = new(true);

    public static EntryVerdict No(string complaint) => new(false, complaint);
}

/// <summary>Something the panel is asking the Commander to say or type (Phase 25, "Say it, or type it").</summary>
/// <param name="Key">The crumb key this becomes.</param>
/// <param name="Word">The crumb word.</param>
/// <param name="Title">What is being entered. "System name".</param>
/// <param name="Context">What it is for, as the header's second line.</param>
/// <param name="Initial">What is in the box to begin with.</param>
/// <param name="Surface">Which surface opens.</param>
/// <param name="Validate">Whether a value is a thing.</param>
/// <param name="Suggestions">
/// Every value the caller would accept, when there are few enough of them to name — the closed list, in
/// the order it should be read.
/// </param>
public sealed record EntryRequest(
    string Key,
    string Word,
    string Title,
    string? Context,
    string Initial,
    EntrySurface Surface,
    Func<string, EntryVerdict>? Validate = null,
    IReadOnlyList<string>? Suggestions = null);

/// <summary>What d47 heard, and how sure it was (Phase 25).</summary>
/// <param name="Text">The transcription.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Final">
/// Whether this is the transcriber's answer or a partial on the way to it.
/// </param>
public sealed record Heard(string Text, double Confidence, bool Final);

/// <summary>The correction loop, as arithmetic (Phase 25, "Say it, or type it").</summary>
public static class TextEntryLoop
{
    /// <summary>Below this, the keyboard comes back rather than a guess being committed.</summary>
    public const double ConfidentEnough = 0.6;

    /// <summary>What to do with what was heard.</summary>
    /// <param name="heard">What came back, or null when the transcriber gave nothing at all.</param>
    /// <param name="validate">
    /// Whether the value is a thing, or null when the caller does not care.
    /// </param>
    /// <param name="verdict">What the caller made of it, when it was asked.</param>
    public static EntryFallback? Judge(
        Heard? heard,
        Func<string, EntryVerdict>? validate,
        out EntryVerdict? verdict)
    {
        verdict = null;

        if (heard is null || !heard.Final || Listening.SpeechNoise.IsNothingSaid(heard.Text))
        {
            // A partial is not a failure and is not an answer.
            return heard is { Final: false } ? null : EntryFallback.NothingHeard;
        }

        if (heard.Confidence < ConfidentEnough)
        {
            return EntryFallback.LowConfidence;
        }

        if (validate is null)
        {
            return null;
        }

        verdict = validate(heard.Text.Trim());

        return verdict.Accepted ? null : EntryFallback.DidNotResolve;
    }

    /// <summary>
    /// What to put above the keyboard when it comes back by itself, so its reappearance reads as an
    /// answer rather than as the microphone having failed silently.
    /// </summary>
    public static string Explain(EntryFallback fallback, string? complaint) => fallback switch
    {
        EntryFallback.NothingHeard => "I did not catch that.",
        EntryFallback.LowConfidence => "I was not sure I heard that correctly.",
        _ => complaint ?? "That did not resolve to anything.",
    };

    /// <summary>
    /// What d47 says back before the value is committed, which is the only check left for the one
    /// failure it cannot detect: confident, valid, and still wrong.
    /// </summary>
    public static string ReadBack(string title, string value) => $"{title}: {value}. Is that right?";
}
