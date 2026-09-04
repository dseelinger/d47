using System.Text.RegularExpressions;

namespace D47.Core.Audio;

/// <summary>
/// Delivery direction a model writes in square brackets — <c>[sighs]</c>, <c>[alarmed]</c> — for a
/// provider that performs it, removed for every provider that would read it out loud
/// (<a href="https://github.com/dseelinger/d47/issues/291">#291</a>).
/// <para>
/// <b>The strip is mandatory rather than tidy, and that is measured.</b> Flash 2.5 was sent the
/// brackets unchanged and transcribed back as <i>"Whispers, cutting the drives"</i>, <i>"Sighs. That
/// is the third interdiction"</i>, <i>"Sarcastic beautiful landing"</i> — every time, on every tag.
/// Kokoro is worse by construction: <see cref="Speech.Phonemiser"/> lists <c>[</c> and <c>]</c>
/// among the brackets it trims, so it pronounces the contents as a word. Four of the five providers
/// are in that position (docs/spikes/elevenlabs-v3-conversational.md §7).
/// </para>
/// <para>
/// <b>There is no fixed vocabulary, deliberately.</b> ElevenLabs publishes examples and says
/// outright that <i>"there are likely many more effective tags beyond this list"</i> — the model
/// reads the bracket as a description rather than looking it up, which is why
/// <c>[grumbles quietly]</c>, in no list anywhere, produced an actual grumble. So d47 does not
/// police the words; it polices <em>where they may go</em>. A tag the service cannot interpret is
/// dropped silently and never spoken, which is the only failure that would reach a Commander's ears.
/// </para>
/// <para>
/// <b>Never spoken means never claimed, either.</b> At d47's sentence lengths a tag is a
/// probability and not a guarantee — ElevenLabs' own word is "inconsistent" — so the spoken-line log
/// records what was <em>asked for</em>. Nothing anywhere may report a delivery as performed.
/// </para>
/// </summary>
public static partial class AudioTags
{
    /// <summary>
    /// The written sentence with its direction removed, and the spacing a removal leaves tidied.
    /// <para>
    /// This is what the caption, the transcript and the conversation history get, whatever the
    /// provider — direction is for the speaker, and a Commander reading <c>[sighs]</c> on screen is
    /// reading stage notes (the maintainer's ruling, 2026-09-04: tags belong in the log and nowhere
    /// else a person reads).
    /// </para>
    /// </summary>
    public static string Strip(string sentence) =>
        Has(sentence) ? Tidy(Tag().Replace(sentence, string.Empty)) : sentence;

    /// <summary>Whether there is any direction in here at all. Cheap, and most sentences have none.</summary>
    public static bool Has(string sentence) =>
        sentence.Contains('[', StringComparison.Ordinal) && Tag().IsMatch(sentence);

    /// <summary>
    /// The direction found, in the order written, without their brackets. For the log line, which
    /// is the one place a tag is allowed to survive.
    /// </summary>
    public static IReadOnlyList<string> In(string sentence) =>
        Has(sentence)
            ? [.. Tag().Matches(sentence).Select(match => match.Groups["tag"].Value)]
            : [];

    /// <summary>
    /// The direction kept only if it will be performed, and removed otherwise. The one call the
    /// pipeline makes, so that "which providers read tags" is answered in a single place rather
    /// than at each of them.
    /// </summary>
    public static string For(string sentence, bool performed) =>
        performed ? sentence : Strip(sentence);

    /// <summary>
    /// A run of spaces where a tag used to be, and a space before punctuation that followed one.
    /// Both are what removal leaves behind rather than anything a model wrote.
    /// </summary>
    private static string Tidy(string sentence) =>
        Gap().Replace(sentence, " ").Replace(" ,", ",", StringComparison.Ordinal).Trim();

    /// <summary>
    /// One bracketed direction.
    /// <para>
    /// <b>Not followed by <c>(</c></b>, because that shape is a markdown link and belongs to
    /// <see cref="PlainSpeech"/> — eating <c>[text]</c> out of <c>[text](url)</c> would leave the
    /// url behind to be read aloud, which is the fault this is here to prevent, arriving from the
    /// other direction.
    /// </para>
    /// <para>
    /// <b>Opening on a letter</b>, so a footnote marker or an array index is not direction. And
    /// <b>bounded at 40 characters with no newline</b>: direction is a word or three, and an
    /// unbounded match across a stray bracket would swallow a sentence.
    /// </para>
    /// </summary>
    [GeneratedRegex(@"\[(?<tag>[A-Za-z][A-Za-z0-9 '’-]{0,39})\](?!\()")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex Gap();
}
