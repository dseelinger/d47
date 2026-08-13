using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// In-game chat, read aloud in somebody else's voice (list.md Phase 11, "Speak incoming
/// messages in another voice").
/// <para>
/// Phase 10 shipped dictating <em>into</em> Elite's chat. Nothing read what came back, so this
/// is the receiving half as well as the re-voicing of it.
/// </para>
/// <para>
/// <b>This is the trust boundary, at its thinnest point.</b> Every string here was written by
/// somebody else — another Commander's name, another Commander's message — and none of it ever
/// reaches the model. It goes to the synthesiser and to the panel, which is the whole of what
/// "re-voice in-game communications" asks for. There is deliberately no path from here into a
/// prompt: architecture.md §7 lists in-game comms as the source whose attacker is "any player
/// in range", and the mitigation is that this text is never treated as instructions.
/// </para>
/// </summary>
public sealed partial class IncomingMessages : ICallout
{
    public string Id => "messages";

    /// <summary>Whether to speak them at all. Off until the Commander says otherwise.</summary>
    public Func<bool> Enabled { get; set; } = () => false;

    /// <summary>
    /// Whether NPC chatter is included. Its own switch because the volume is completely
    /// different — a station approach produces a steady stream of it.
    /// </summary>
    public Func<bool> IncludeNpcs { get; set; } = () => false;

    /// <summary>
    /// The Commander's own name, so their own messages are not read back to them. Elite echoes
    /// what you send on the same channel it went out on.
    /// </summary>
    public string? CommanderName { get; set; }

    /// <summary>
    /// Channels carrying messages a person typed. <c>starsystem</c> is included because system
    /// chat is players; <c>npc</c> is the only channel that is not, which is why it is the one
    /// with its own switch.
    /// </summary>
    private static readonly HashSet<string> PlayerChannels =
        new(StringComparer.OrdinalIgnoreCase) { "player", "wing", "local", "friend", "squadron", "starsystem" };

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Never from the backlog. Starting d47 after an hour of flying should not read out an
        // hour of chat, for the same reason the material milestones prime silently.
        if (context.IsPriming || !Enabled())
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind is not "ReceiveText")
            {
                continue;
            }

            if (Read(journalEvent) is { } message)
            {
                yield return message;
            }
        }
    }

    /// <summary>
    /// One message, or null if it is not one to speak. Separated from the loop so the whole of
    /// the decision is testable against a single event.
    /// </summary>
    public Announcement? Read(JournalEvent journalEvent)
    {
        var channel = journalEvent.String("Channel") ?? "npc";
        var isPlayer = PlayerChannels.Contains(channel);

        if (!isPlayer && !IncludeNpcs())
        {
            return null;
        }

        // The localised form or nothing. An unlocalised value is one of Elite's `$id;` tokens,
        // and speaking "dollar pirate underscore attack semicolon" aloud is worse than silence
        // — this is also how the danger callouts treat them, as ids rather than as prose.
        var text = journalEvent.String("Message_Localised") ?? journalEvent.String("Message");

        if (text is null || Token().IsMatch(text))
        {
            return null;
        }

        var sender = Undecorate(
            journalEvent.String("From_Localised") ?? journalEvent.String("From") ?? "Unknown");

        // Elite echoes what you send back to you on the same channel. Hearing your own message
        // read to you in a stranger's voice is the single most confusing thing this could do.
        if (CommanderName is { Length: > 0 } own
            && sender.Contains(own, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new Announcement($"message.{channel}", $"{sender} says: {text}")
        {
            Voice = VoiceRole.Comms,
            Speaker = sender,
            SpeakerIsPlayer = isPlayer,

            // No cooldown. Two messages from the same channel are two messages, not one warning
            // said twice — the cooldown exists for conditions that stay true across hundreds of
            // ticks, and a chat line is an event.
            Cooldown = TimeSpan.Zero,
        };
    }

    /// <summary>
    /// Elite wraps names in localisation decorators: <c>$cmdr_decorate:#name=Vex;</c> and
    /// <c>$npc_name_decorate:#name=Ilse Bruhn;</c>. The name is the interesting part and the
    /// scaffolding is not something to say out loud.
    /// </summary>
    public static string Undecorate(string from)
    {
        var named = Decorated().Match(from);

        if (named.Success)
        {
            return named.Groups["name"].Value.Trim();
        }

        // A bare `$Something;` with no name inside it — a ship or faction id. Strip the
        // punctuation and give back something sayable rather than the token.
        return from.Trim('$', ';', ' ').Replace('_', ' ').Trim();
    }

    [GeneratedRegex(@"#name=(?<name>[^;]+);")]
    private static partial Regex Decorated();

    /// <summary>
    /// An unlocalised token: starts with <c>$</c> and ends with <c>;</c>, with no spaces that
    /// would suggest it is prose someone typed.
    /// </summary>
    [GeneratedRegex(@"^\$[^\s]*;?$")]
    private static partial Regex Token();
}
