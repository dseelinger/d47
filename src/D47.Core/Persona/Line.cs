using D47.Core.Audio;
using D47.Core.Conversation;

namespace D47.Core.Persona;

/// <summary>Someone other than the ship's AI who answers a turn addressed to them.</summary>
/// <param name="Transcript">The speaker's own conversation, which the ship AI's never shares.</param>
/// <param name="OffersTools">Whether the model is offered the mode-free tool list on this speaker's turns.</param>
/// <param name="Signal">How clearly the line carries, 0 to 1.</param>
/// <param name="Model">The model this speaker answers on, or null for the turn loop's own.</param>
/// <param name="Screen">
/// Given the whole reply, returns what may be said instead; the reply is held back until it has run.
/// </param>
/// <param name="HiddenStory">This speaker's reading of the running story's hidden layer, in place of the ship AI's.</param>
/// <param name="Cast">The id of the running story's cast member speaking, or null.</param>
/// <param name="LiveState">What this speaker knows of the game right now, in place of the ship AI's live game state.</param>
/// <param name="Voice">The speaker's own voice, or null for their role's.</param>
/// <param name="Picture">The picture name the speaker's replies are shown with, or null to choose one by role.</param>
public sealed record Speaker(
    VoiceRole Role,
    string Name,
    string Brief,
    List<ConversationMessage> Transcript,
    bool OffersTools,
    double Signal,
    string? Model = null,
    Func<string, string>? Screen = null,
    Func<string?>? HiddenStory = null,
    string? Cast = null,
    Func<string?>? LiveState = null,
    Characters.CharacterVoice? Voice = null,
    string? Picture = null);

/// <summary>What a line makes of one utterance.</summary>
public abstract record LineDecision
{
    private LineDecision()
    {
    }

    /// <summary>Not a party this line speaks for.</summary>
    public sealed record NotMine : LineDecision;

    /// <summary>This speaker answers <paramref name="Question"/>.</summary>
    public sealed record Taken(Speaker Speaker, string Question) : LineDecision;

    /// <summary>Answered by the ship's AI with a fixed line, no model call.</summary>
    public sealed record Refused(string Line, TurnRoute Route) : LineDecision;
}

/// <summary>The lines of an exchange a speaker's brief carries: the latest <see cref="Most"/>, oldest first.</summary>
public static class ExchangeLines
{
    public const int Most = 20;

    public static string Recent(IEnumerable<string> said) => string.Join('\n', said.TakeLast(Most));
}

/// <summary>Someone on a comms channel, who answers a turn spoken through the comms push-to-talk while selected.</summary>
public interface ILine
{
    /// <summary>The answer for <paramref name="selected"/>; <see cref="LineDecision.NotMine"/> for a party this line does not speak for.</summary>
    Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken);
}
