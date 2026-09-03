namespace D47.Core.Interface;

/// <summary>
/// Which way of entering text opens first (Phase 25, "Say it, or type it").
/// <para>
/// <b>Declared per call site</b>, exactly as <see cref="ChoiceSurface"/> is, and for a reason
/// that is about the value rather than about the control: a system name is far easier said than
/// typed, and a number is the reverse.
/// </para>
/// </summary>
public enum EntrySurface
{
    /// <summary>
    /// d47 listens, shows what it hears as it hears it, and reads back what it got. The keyboard
    /// is still there and is one press away; it simply is not what opens.
    /// </summary>
    Voice,

    /// <summary>The drawn keyboard, straight away. What a hotkey and a number get.</summary>
    Keyboard,
}

/// <summary>
/// Why the keyboard came back on its own (Phase 25).
/// <para>
/// <b>The three failures d47 can detect</b>, and the list is exactly that long on purpose.
/// Anything outside it — confident, valid, and still not what the Commander meant — is not
/// detectable from here at all, and that is what the readback is for.
/// </para>
/// </summary>
public enum EntryFallback
{
    /// <summary>Nothing came back at all. A held key with no speech behind it, or a dead device.</summary>
    NothingHeard,

    /// <summary>Something came back and the transcriber was not sure of it.</summary>
    LowConfidence,

    /// <summary>
    /// A confident transcription of a value that is not a thing. Real rather than theoretical:
    /// the two-resolver check already catches a renamed system and an invented one
    /// (Phase 23).
    /// </summary>
    DidNotResolve,
}

/// <summary>What a caller made of a value it was offered.</summary>
/// <param name="Accepted">Whether it is a thing.</param>
/// <param name="Complaint">
/// What was wrong with it, in the Commander's terms, for the keyboard to open under. Null when it
/// was accepted, and required when it was not — "that is not a system" is the whole difference
/// between a keyboard that reappeared and a keyboard that reappeared for a reason.
/// </param>
public sealed record EntryVerdict(bool Accepted, string? Complaint = null)
{
    public static readonly EntryVerdict Ok = new(true);

    public static EntryVerdict No(string complaint) => new(false, complaint);
}

/// <summary>
/// Something the panel is asking the Commander to say or type (Phase 25, "Say it, or type
/// it").
/// </summary>
/// <param name="Key">The crumb key this becomes.</param>
/// <param name="Word">The crumb word.</param>
/// <param name="Title">What is being entered. "System name".</param>
/// <param name="Context">What it is for, as the header's second line.</param>
/// <param name="Initial">What is in the box to begin with. Empty for a value being set fresh.</param>
/// <param name="Surface">Which surface opens. See <see cref="EntrySurface"/>.</param>
/// <param name="Validate">
/// Whether a value is a thing. Called before the value is accepted and again for each correction;
/// a refusal is one of the three detectable failures and puts the keyboard back with the
/// complaint showing.
/// </param>
/// <param name="Suggestions">
/// Every value the caller would accept, when there are few enough of them to show — the closed
/// list, in the order it should be read. Null for a genuinely open value, which is most of them.
/// <para>
/// <b>Not a surface.</b> Voice still opens where the call site asked for voice; the list sits
/// under the box for whoever is at a mouse, narrowing as they type, so a hull is picked rather
/// than guessed at and told after the fact that it was wrong (#282).
/// </para>
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

/// <summary>
/// Narrowing a closed list as the Commander types (#282).
/// <para>
/// <b>Narrowing rather than resolving</b>, which is the rule the searchable chooser already
/// keeps: nothing here refuses a value or asks for a spelling, so a query that matches nothing
/// leaves an empty list and the box still holds what was typed. In Core with the rest of the
/// panel arithmetic, because it is a decision about words rather than about controls.
/// </para>
/// </summary>
public static class EntrySuggestions
{
    /// <summary>
    /// The values worth showing for what has been typed so far. Everything for an empty query,
    /// and otherwise every value the query appears anywhere in — "type" finds the Type-9 as well
    /// as the Type-6, and "mk" finds every mark of everything.
    /// </summary>
    public static IReadOnlyList<string> Narrow(IReadOnlyList<string>? all, string? query)
    {
        if (all is null || all.Count == 0)
        {
            return [];
        }

        var wanted = query?.Trim() ?? string.Empty;

        return wanted.Length == 0
            ? all
            : [.. all.Where(value => value.Contains(wanted, StringComparison.OrdinalIgnoreCase))];
    }
}

/// <summary>
/// What d47 heard, and how sure it was (Phase 25).
/// </summary>
/// <param name="Text">The transcription. Empty when nothing was heard.</param>
/// <param name="Confidence">
/// 0 to 1. Below <see cref="TextEntryLoop.ConfidentEnough"/> the keyboard comes back rather than
/// a guess being committed.
/// </param>
/// <param name="Final">
/// Whether this is the transcriber's answer or a partial on the way to it. Partials are shown and
/// never committed — a dismissed keyboard needs a <b>visible listening state</b> with the partial
/// showing, or the Commander is talking at a blank page.
/// </param>
public sealed record Heard(string Text, double Confidence, bool Final);

/// <summary>
/// The correction loop, as arithmetic (Phase 25, "Say it, or type it").
/// <para>
/// <b>The same loop the timers use</b>: d47 shows and says what it heard, and the Commander
/// confirms or corrects. That also satisfies the rule <c>69fbd3e</c> already set — what is
/// entered reaches the box <b>once, on Done</b>, because a system name arriving letter by letter
/// is eleven wrong values on the way to the right one. A spoken value is naturally atomic and
/// fits that better than typing does.
/// </para>
/// <para>
/// Pure, and in Core with the rest of the panel arithmetic, because the interesting half of this
/// is a decision rather than a control: given what was heard and what the caller made of it,
/// does the keyboard come back, and saying why. A test can walk every branch with no microphone,
/// no transcriber and no surface.
/// </para>
/// </summary>
public static class TextEntryLoop
{
    /// <summary>
    /// Below this, the keyboard comes back rather than a guess being committed.
    /// <para>
    /// Deliberately not tuned to a corpus. The cost of the two mistakes is lopsided — a keyboard
    /// that reappears when it need not have costs one press, and a wrong value committed
    /// confidently costs a plan pointed at the wrong system — so this sits where an uncertain
    /// transcription is treated as a failure rather than as a probably.
    /// </para>
    /// </summary>
    public const double ConfidentEnough = 0.6;

    /// <summary>
    /// What to do with what was heard.
    /// <para>
    /// Returns the fallback that puts the keyboard back, or null to accept the value. A partial
    /// is neither: it is shown and nothing else, which is what the third state is for.
    /// </para>
    /// </summary>
    /// <param name="heard">What came back, or null when the transcriber gave nothing at all.</param>
    /// <param name="validate">Whether the value is a thing, or null when the caller does not care.</param>
    /// <param name="verdict">What the caller made of it, when it was asked.</param>
    public static EntryFallback? Judge(
        Heard? heard,
        Func<string, EntryVerdict>? validate,
        out EntryVerdict? verdict)
    {
        verdict = null;

        if (heard is null || !heard.Final || Listening.SpeechNoise.IsNothingSaid(heard.Text))
        {
            // A partial is not a failure and is not an answer. Callers distinguish the two by
            // whether the loop was asked at all; this is the "heard nothing" case, and a partial
            // never reaches here because a partial is never judged.
            //
            // Heard nothing includes a transcription that is only Whisper describing the room —
            // "(mouse clicking)" is not the value somebody was asked for, and committing it would
            // put it in a plan (remediation.md 14, item 8).
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
    /// What to put above the keyboard when it comes back by itself, so its reappearance reads as
    /// an answer rather than as the microphone having failed silently.
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
