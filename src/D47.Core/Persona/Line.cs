using D47.Core.Audio;
using D47.Core.Conversation;

namespace D47.Core.Persona;

/// <summary>Someone other than the ship's AI who answers a turn addressed to them.</summary>
/// <param name="Transcript">The speaker's own conversation, which the ship AI's never shares.</param>
/// <param name="OffersTools">Whether the model is offered the mode-free tool list on this speaker's turns.</param>
/// <param name="Signal">How clearly the line carries, 0 to 1.</param>
public sealed record Speaker(
    VoiceRole Role,
    string Name,
    string Brief,
    List<ConversationMessage> Transcript,
    bool OffersTools,
    double Signal);

/// <summary>What a line makes of one utterance.</summary>
public abstract record LineDecision
{
    private LineDecision()
    {
    }

    /// <summary>Not addressed to this line.</summary>
    public sealed record NotMine : LineDecision;

    /// <summary>This speaker answers <paramref name="Question"/>.</summary>
    public sealed record Taken(Speaker Speaker, string Question) : LineDecision;

    /// <summary>Addressed to this line and answered by the ship's AI with a fixed line, no model call.</summary>
    public sealed record Refused(string Line, TurnRoute Route) : LineDecision;
}

/// <summary>An addressed speaker <see cref="TurnLoop"/> asks before any of its own routes.</summary>
public interface ILine
{
    /// <summary>Whether this line takes every turn until it closes, with no name needed.</summary>
    bool IsOpen { get; }

    Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken);

    void Close();
}
