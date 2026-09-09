using System.Globalization;
using System.Text;

namespace D47.Core.Listening;

/// <summary>What the wake-word policy decided about one transcribed utterance.</summary>
public enum WakeOutcome
{
    /// <summary>Not addressed to d47.</summary>
    Ignored,

    /// <summary>The name and nothing after it. d47 answers, and listens for what comes next.</summary>
    Woken,

    /// <summary>Addressed, with something to do.</summary>
    Addressed,
}

/// <summary><param name="Text"> What was actually said to d47, with the name taken off the front.</summary>
/// <param name="Text">What was actually said to d47, with the name taken off the front.</param>
public readonly record struct WakeDecision(WakeOutcome Outcome, string Text);

/// <summary>Wake-word gating (Phase 13), as another policy over the same audio stream.</summary>
public sealed class WakeWordGate
{
    private readonly Lock _gate = new();
    private string[] _flattened = [];
    private DateTimeOffset? _awake;

    /// <summary>How far into an utterance the name may appear and still count as being addressed.</summary>
    public int Lead { get; set; } = 12;

    /// <summary>How long d47 keeps listening after answering to its name with nothing after it.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(12);

    /// <summary>The names it answers to.</summary>
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

    /// <summary>Shuts the window.</summary>
    public void Sleep()
    {
        lock (_gate)
        {
            _awake = null;
        }
    }

    /// <summary>
    /// Decides whether <paramref name="transcript"/> was addressed to d47, and returns what is left of
    /// it once the name is taken off.
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
                    // The name and nothing else.
                    _awake = now;
                    return new WakeDecision(WakeOutcome.Woken, string.Empty);
                }

                _awake = null;
                return new WakeDecision(WakeOutcome.Addressed, rest);
            }

            if (_awake is { } since && now - since < Window)
            {
                // Inside the window opened by a bare wake word, so this is the sentence that was coming.
                _awake = null;
                return new WakeDecision(WakeOutcome.Addressed, said);
            }

            return new WakeDecision(WakeOutcome.Ignored, string.Empty);
        }
    }

    /// <summary>
    /// Where the wake phrase ends in <paramref name="said"/>, or null if it is not near the front of
    /// it.
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

    /// <summary>Letters and digits only, folded to lower case.</summary>
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
