using System.Globalization;
using System.Text;

namespace D47.Core.Listening;

/// <summary>What the wake-word policy decided about one transcribed utterance.</summary>
public enum WakeOutcome
{
    /// <summary>Not addressed to d47. Dropped without a word about it.</summary>
    Ignored,

    /// <summary>The name and nothing after it. d47 answers, and listens for what comes next.</summary>
    Woken,

    /// <summary>Addressed, with something to do. <see cref="WakeDecision.Text"/> is the rest.</summary>
    Addressed,
}

/// <param name="Text">
/// What was actually said to d47, with the name taken off the front. Empty for
/// <see cref="WakeOutcome.Woken"/> and for <see cref="WakeOutcome.Ignored"/>.
/// </param>
public readonly record struct WakeDecision(WakeOutcome Outcome, string Text);

/// <summary>
/// Wake-word gating (list.md Phase 13), as another policy over the same audio stream.
/// <para>
/// <b>It matches words, not audio, and that is the design rather than a shortcut.</b> The usual
/// shape for this is a small always-on acoustic model listening for one phrase — which means a
/// second model to ship, a second thing to download, a fixed vocabulary chosen at build time,
/// and a keyword that cannot be the name the Commander gave their ship's AI. d47 already has a
/// speech model and a detector that says when a sentence started and stopped, so the wake word
/// is the cheapest possible thing on top of both: the voice-activity gate segments the stream,
/// Whisper turns a segment into words, and this decides whether those words were addressed to
/// d47 at all. Change the name in settings and it is the wake word, with nothing to retrain.
/// </para>
/// <para>
/// The cost is stated rather than hidden: every stretch of speech in the room is transcribed
/// before it can be discarded, so this asks more of the CPU than push-to-talk does, and the
/// smaller models are the ones to run it on. Nothing is sent anywhere — transcription is local
/// (architecture.md §10, "Cloud STT" rejected) — so what it costs is cycles, not egress.
/// </para>
/// <para>
/// <b>Matching is forgiving on purpose.</b> Whisper writes a short name however it hears it:
/// "D47" arrives as <c>D47</c>, <c>D 47</c>, <c>d-47</c>, <c>The 47</c>. So the comparison is
/// made on a flattened form with punctuation and spacing removed, the phrase may sit anywhere
/// in the opening few words rather than strictly first, and every name the ship's AI answers to
/// is a phrase. A name it hears as something else entirely is a name the Commander can add.
/// </para>
/// <para>
/// Reads no clock: the window after a bare wake word is measured from a time handed in, like
/// everything else in Core.
/// </para>
/// </summary>
public sealed class WakeWordGate
{
    private readonly Lock _gate = new();
    private string[] _flattened = [];
    private DateTimeOffset? _awake;

    /// <summary>
    /// How far into an utterance the name may appear and still count as being addressed.
    /// <para>
    /// Not zero, because a transcript rarely starts where the Commander did: Whisper prepends
    /// what it made of the breath before the word, and the pre-roll deliberately includes audio
    /// from before the sentence. Not unbounded either, or "I was talking to Sarah about d47"
    /// is a command.
    /// </para>
    /// </summary>
    public int Lead { get; set; } = 12;

    /// <summary>
    /// How long d47 keeps listening after answering to its name with nothing after it.
    /// <para>
    /// This is what makes "d47?" — "yes, Commander" — "how much fuel do I have" work, which is
    /// how a person addresses anybody. Without it the Commander has to say the name again into
    /// a companion that has just confirmed it is listening.
    /// </para>
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(12);

    /// <summary>
    /// The names it answers to. Set from the ship's AI name and whatever else the Commander
    /// added, so renaming the core renames the wake word with nothing to rebuild.
    /// </summary>
    public IReadOnlyList<string> Phrases
    {
        get
        {
            lock (_gate)
            {
                return _phrases;
            }
        }

        set
        {
            lock (_gate)
            {
                _phrases = [.. value.Where(phrase => !string.IsNullOrWhiteSpace(phrase))];
                _flattened = [.. _phrases.Select(Flatten).Where(phrase => phrase.Length > 0)];
            }
        }
    }

    private IReadOnlyList<string> _phrases = [];

    /// <summary>Whether the window from a bare wake word is still open at <paramref name="now"/>.</summary>
    public bool IsAwake(DateTimeOffset now)
    {
        lock (_gate)
        {
            return _awake is { } since && now - since < Window;
        }
    }

    /// <summary>
    /// Shuts the window. Called when the Commander is answered, so the next sentence in the room
    /// needs the name again rather than inheriting the last one.
    /// </summary>
    public void Sleep()
    {
        lock (_gate)
        {
            _awake = null;
        }
    }

    /// <summary>
    /// Decides whether <paramref name="transcript"/> was addressed to d47, and returns what is
    /// left of it once the name is taken off.
    /// <para>
    /// With no phrases configured everything is addressed — which is what "continuous listening
    /// with no wake word" means, and is the voice-activity mode. This class then costs nothing
    /// and decides nothing, rather than the caller having to know which policy is in force.
    /// </para>
    /// </summary>
    public WakeDecision Admit(string transcript, DateTimeOffset now)
    {
        var said = transcript.Trim();

        lock (_gate)
        {
            if (_flattened.Length == 0)
            {
                return new WakeDecision(WakeOutcome.Addressed, said);
            }

            if (Locate(said) is { } found)
            {
                var rest = said[found..].TrimStart(' ', ',', '.', '!', '?', ':', ';', '-');

                if (rest.Length == 0)
                {
                    // The name and nothing else. Answer, and stay listening for what it was
                    // being said in front of.
                    _awake = now;
                    return new WakeDecision(WakeOutcome.Woken, string.Empty);
                }

                _awake = null;
                return new WakeDecision(WakeOutcome.Addressed, rest);
            }

            if (_awake is { } since && now - since < Window)
            {
                // Inside the window opened by a bare wake word, so this is the sentence that
                // was coming. It closes the window: one follow-up, not an open microphone.
                _awake = null;
                return new WakeDecision(WakeOutcome.Addressed, said);
            }

            return new WakeDecision(WakeOutcome.Ignored, string.Empty);
        }
    }

    /// <summary>
    /// Where the wake phrase ends in <paramref name="said"/>, or null if it is not near the
    /// front of it.
    /// <para>
    /// Both the transcript and the phrases are compared flattened — letters and digits only,
    /// folded to lower case — because that is the one form in which <c>D 47</c>, <c>d-47</c> and
    /// <c>D47.</c> are the same word. The offset is then mapped back onto the original string,
    /// so what is handed on keeps the Commander's own punctuation and spacing.
    /// </para>
    /// </summary>
    private int? Locate(string said)
    {
        var map = new List<int>(said.Length);
        var flat = new StringBuilder(said.Length);

        for (var i = 0; i < said.Length; i++)
        {
            if (!char.IsLetterOrDigit(said[i]))
            {
                continue;
            }

            flat.Append(char.ToLowerInvariant(said[i]));
            map.Add(i);
        }

        var haystack = flat.ToString();

        foreach (var phrase in _flattened)
        {
            var at = haystack.IndexOf(phrase, StringComparison.Ordinal);

            if (at < 0 || at > Lead)
            {
                continue;
            }

            // One past the last character the phrase consumed, in the original string.
            return map[at + phrase.Length - 1] + 1;
        }

        return null;
    }

    /// <summary>Letters and digits only, folded to lower case. Culture-invariant by design.</summary>
    private static string Flatten(string phrase)
    {
        var flat = new StringBuilder(phrase.Length);

        foreach (var character in phrase)
        {
            if (char.IsLetterOrDigit(character))
            {
                flat.Append(char.ToLower(character, CultureInfo.InvariantCulture));
            }
        }

        return flat.ToString();
    }
}
