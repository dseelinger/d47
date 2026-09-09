namespace D47.Core.Debrief;

/// <summary>
/// Who said one line of a session, and the whole of the poisoning defence at extraction time (#162).
/// </summary>
public enum DebriefSpeaker
{
    /// <summary>The Commander, however they said it — spoken, typed, or through a switch.</summary>
    Commander,

    /// <summary>d47's own reply.</summary>
    Ship,

    /// <summary>
    /// Anything that came from outside the two of them: an in-game message read out, a journal line, a
    /// web result quoted back.
    /// </summary>
    Game,
}

/// <summary>One line of a session, as the debrief pass reads it.</summary>
/// <param name="When">When it was said.</param>
/// <param name="Who">Which of the three voices it was.</param>
/// <param name="Text">What was said, verbatim.</param>
/// <param name="Clip">
/// The audio recorder's row id for this line, where the recorder was running (#164).
/// </param>
public sealed record DebriefLine(DateTimeOffset When, DebriefSpeaker Who, string Text, string? Clip = null);

/// <summary>What a session sounded like, held for the length of that session and no longer (#162).</summary>
public sealed class DebriefSession(int capacity = DebriefSession.DefaultCapacity)
{
    /// <summary>How many lines one session may hold.</summary>
    public const int DefaultCapacity = 2_000;

    /// <summary>The longest line kept.</summary>
    public const int MaxLineLength = 2_000;

    private readonly Lock _gate = new();
    private readonly Queue<DebriefLine> _lines = new();
    private readonly int _capacity = capacity > 0 ? capacity : DefaultCapacity;

    /// <summary>Everything still held, oldest first.</summary>
    public IReadOnlyList<DebriefLine> Lines
    {
        get
        {
            lock (_gate)
            {
                return [.. _lines];
            }
        }
    }

    /// <summary>Writes one line down.</summary>
    public void Say(DateTimeOffset when, DebriefSpeaker who, string text, string? clip = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var trimmed = text.Trim();

        if (trimmed.Length > MaxLineLength)
        {
            trimmed = trimmed[..MaxLineLength];
        }

        lock (_gate)
        {
            _lines.Enqueue(new DebriefLine(when, who, trimmed, clip));

            while (_lines.Count > _capacity)
            {
                _lines.Dequeue();
            }
        }
    }

    /// <summary>Forgets everything.</summary>
    public void Empty()
    {
        lock (_gate)
        {
            _lines.Clear();
        }
    }
}

/// <summary>The kinds of feedback nobody typed (#162).</summary>
public enum DebriefSignalKind
{
    /// <summary>d47 was speaking and was stopped before it finished.</summary>
    SpeechCutOff,

    /// <summary>A warning fired and was switched off within seconds of firing.</summary>
    WarningDisabledSoonAfter,
}

/// <summary>One thing that happened which nobody said anything about.</summary>
/// <param name="When">The most recent occurrence, for ordering the questions.</param>
/// <param name="Kind">Which reading is being offered, never taken.</param>
/// <param name="What">
/// The subject, in the words a Commander would recognise — "the fuel warning", "a reply about
/// engineering".
/// </param>
/// <param name="Count">How many times.</param>
public sealed record DebriefSignal(DateTimeOffset When, DebriefSignalKind Kind, string What, int Count = 1);
