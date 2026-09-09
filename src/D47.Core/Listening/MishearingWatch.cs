namespace D47.Core.Listening;

/// <summary>
/// A name that would not resolve, held just long enough to find out what the Commander meant (#134).
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
    /// </summary>
    public bool Rejected(string spoken)
    {
        var token = SoundsLike.Token(spoken);

        lock (_gate)
        {
            // A second failure, whether or not it is the same word: the Commander answered and was misheard
            // again, and another round of "did you mean" is the loop.
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

    /// <summary>A name resolved.</summary>
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

    /// <summary>The sentence a name that would not resolve gets (#134).</summary>
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
