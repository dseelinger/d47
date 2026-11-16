using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
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
/// <param name="ScreenReply">
/// Given the whole reply, returns what may be said and its English; used in place of <paramref name="Screen"/> for a
/// speaker who answers in another language.
/// </param>
/// <param name="HiddenStory">This speaker's reading of the running story's hidden layer, in place of the ship AI's.</param>
/// <param name="Cast">The id of the running story's cast member speaking, or null.</param>
/// <param name="LiveState">What this speaker knows of the game right now, in place of the ship AI's live game state.</param>
/// <param name="Voice">The speaker's own voice, or null for their role's.</param>
/// <param name="Picture">The picture name the speaker's replies are shown with, or null to choose one by role.</param>
/// <param name="HeardAs">
/// The incoming message a comms speaker's replies are voiced as, built by <see cref="Callouts.IncomingMessages.VoicedAs"/>,
/// or null for any other speaker.
/// </param>
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
    string? Picture = null,
    Callouts.Announcement? HeardAs = null,
    Func<string, ScreenedReply>? ScreenReply = null);

/// <summary>What may be said of a reply, and its English when it is said in another language; Spoken is empty when nothing may be said.</summary>
public sealed record ScreenedReply(string Spoken, string? Translation = null);

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

    /// <summary>Records an exchange said to everyone on the party's channel. ownReply is the index of the party's own reply, or null when it did not answer.</summary>
    void Overhear(CommsSelection party, CommsExchange exchange, int? ownReply) { }
}

/// <summary>What the Commander asked everyone on a channel and the replies, in the order spoken.</summary>
public sealed record CommsExchange(CommsChannel Channel, string Asked, IReadOnlyList<CommsReply> Replies);

/// <summary>One party's reply within a <see cref="CommsExchange"/>.</summary>
public sealed record CommsReply(string Name, string Said);

/// <summary>The lines a party keeps of an exchange said to everyone on its channel.</summary>
public static class Overheard
{
    /// <summary>For a line with a transcript: the Commander's words when the party did not answer, then the replies it lacks.</summary>
    public static IEnumerable<string> ForTranscript(CommsExchange exchange, int? ownReply)
    {
        if (ownReply is null)
        {
            foreach (var line in Commander(exchange))
            {
                yield return line;
            }
        }

        for (var i = ownReply + 1 ?? 0; i < exchange.Replies.Count; i++)
        {
            if (Reply(exchange.Replies[i]) is { } line)
            {
                yield return line;
            }
        }
    }

    /// <summary>For a line whose exchange already holds the Commander's words and its own reply: every other reply.</summary>
    public static IEnumerable<string> ForExchange(CommsExchange exchange, int? ownReply)
    {
        for (var i = 0; i < exchange.Replies.Count; i++)
        {
            if (i != ownReply && Reply(exchange.Replies[i]) is { } line)
            {
                yield return line;
            }
        }
    }

    /// <summary>Appends <paramref name="lines"/> to <paramref name="held"/>, keeping the last <see cref="ExchangeLines.Most"/>.</summary>
    public static void Keep(List<string> held, IEnumerable<string> lines)
    {
        held.AddRange(lines);

        if (held.Count > ExchangeLines.Most)
        {
            held.RemoveRange(0, held.Count - ExchangeLines.Most);
        }
    }

    /// <summary>The brief block for lines overheard on <paramref name="channel"/>; empty when there are none.</summary>
    public static string Block(CommsChannel channel, IReadOnlyList<string> held) =>
        held.Count == 0
            ? string.Empty
            : $"\n\nSaid to everyone on the {CommsCapability.ChannelNames[channel]} channel, oldest first:\n{ExchangeLines.Recent(held)}";

    private static IEnumerable<string> Commander(CommsExchange exchange) =>
        exchange.Asked.Trim() is { Length: > 0 } asked ? [$"Commander: {asked}"] : [];

    private static string? Reply(CommsReply reply) =>
        reply.Said.Trim() is { Length: > 0 } said ? $"{reply.Name}: {said}" : null;
}
