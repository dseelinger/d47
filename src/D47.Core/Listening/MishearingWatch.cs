namespace D47.Core.Listening;

/// <summary>
/// A name that would not resolve, held just long enough to find out what the Commander meant
/// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
/// <para>
/// <b>The confirmation is the retry, and that is the whole trick.</b> The issue asks for three
/// things — ask for the spelling, re-run when confirmed, remember it — and the middle one needs no
/// new dialogue machinery at all: the failing lookup hands back a sentence that invites a
/// correction, the Commander gives one, and the model re-runs the same tool with the corrected
/// name because that is what it was asked to do. What is left for this to do is notice that the
/// second call succeeded where the first failed, and hand the pair over to be learned.
/// </para>
/// <para>
/// <b>Learned from a name that actually resolved, never from a name that was offered.</b> A near
/// miss d47 suggested is a guess; a lookup that came back with a real answer is the Commander
/// having steered it there. Only the second is evidence.
/// </para>
/// <para>
/// <b>One retry, then it asks rather than looping.</b> A correction is itself spoken and can
/// itself be misheard — so a second failure drops what was outstanding and changes the wording
/// from <i>did you mean</i> to <i>spell it out</i>. Without that, two people who cannot hear each
/// other repeat themselves indefinitely.
/// </para>
/// <para>
/// <b>Per process and never written down.</b> This is the state of one conversation, not of an
/// installation: an outstanding rejection that survived a restart would attach a correction to a
/// question asked last week.
/// </para>
/// </summary>
public sealed class MishearingWatch
{
    private readonly Lock _gate = new();

    private string? _outstanding;
    private bool _asked;

    /// <summary>Whether a correction is being waited on, for the wording to know which to use.</summary>
    public bool Waiting
    {
        get
        {
            lock (_gate)
            {
                return _outstanding is not null;
            }
        }
    }

    /// <summary>
    /// Records a name that did not resolve, and answers whether this is the first time of asking.
    /// <para>
    /// False means the Commander has already been asked once and the answer failed too — the
    /// caller should ask them to spell it out or type it rather than offering another list, and
    /// nothing further is held.
    /// </para>
    /// </summary>
    public bool Rejected(string spoken)
    {
        var token = SoundsLike.Token(spoken);

        lock (_gate)
        {
            // A second failure, whether or not it is the same word: the Commander answered and
            // was misheard again, and another round of "did you mean" is the loop.
            if (_asked)
            {
                _outstanding = null;
                _asked = false;
                return false;
            }

            _outstanding = token;
            _asked = true;

            return true;
        }
    }

    /// <summary>
    /// A name resolved. Answers the correction to learn, or null where there is nothing to learn.
    /// <para>
    /// <b>Clears whatever was outstanding either way.</b> A lookup that worked is the end of the
    /// exchange, so a rejection older than it must not be able to attach itself to the next one.
    /// </para>
    /// </summary>
    public (string Heard, string Meant)? Confirmed(string resolved)
    {
        lock (_gate)
        {
            var heard = _outstanding;

            _outstanding = null;
            _asked = false;

            return heard is not null && SoundsLike.Token(resolved) is { } meant
                   && !string.Equals(heard, meant, StringComparison.OrdinalIgnoreCase)
                ? (heard, meant)
                : null;
        }
    }

    /// <summary>
    /// The sentence a name that would not resolve gets
    /// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>).
    /// <para>
    /// <b>It asks, where the old wording stopped.</b> The reported failure was a polite dead end —
    /// <i>"could be a misspelling — worth double-checking the name"</i> — which leaves the
    /// Commander to notice the problem, work out the spelling and say the whole question again. A
    /// polite dead end is still a dead end.
    /// </para>
    /// <para>
    /// <b>The near names go in the sentence, so the model offers them rather than inventing an
    /// apology.</b> Where there are none — and where the Commander has already been asked once —
    /// it asks for the letters instead, which is the only thing left that a microphone cannot
    /// mangle the same way twice.
    /// </para>
    /// </summary>
    public static string Ask(string kind, string spoken, IReadOnlyList<string> near, bool firstTime)
    {
        ArgumentNullException.ThrowIfNull(near);

        if (!firstTime)
        {
            return $"I still can't find a {kind} called '{spoken}'. Spell it out for me a letter "
                   + "at a time, or type it into the ask box.";
        }

        return near.Count > 0
            ? $"I don't know a {kind} called '{spoken}'. Did you mean {string.Join(", ", near)}? "
              + "Say which one and I will run it again."
            : $"I don't know a {kind} called '{spoken}', and nothing you have visited sounds like "
              + "it. Spell it out for me and I will run it again.";
    }
}
