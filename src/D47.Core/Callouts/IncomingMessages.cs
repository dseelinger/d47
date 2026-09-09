using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// In-game chat, read aloud in somebody else's voice (Phase 11, "Speak incoming messages in another
/// voice").
/// </summary>
public sealed partial class IncomingMessages : ICallout
{
    public string Id => "messages";

    /// <summary>Whether to speak them at all.</summary>
    public Func<bool> Enabled { get; set; } = () => false;

    /// <summary>Whether NPC chatter is included.</summary>
    public Func<bool> IncludeNpcs { get; set; } = () => false;

    /// <summary>Whether one player channel is spoken, asked with the raw <c>Channel</c> value (#299).</summary>
    public Func<string, bool> ChannelEnabled { get; set; } = _ => true;

    /// <summary>The Commander's own name, so their own messages are not read back to them.</summary>
    public string? CommanderName { get; set; }

    /// <summary>
    /// The Commander's own fleet carrier, so a message from it comes in the tower's voice rather than a
    /// stranger's (#28).
    /// </summary>
    public string? CarrierName { get; set; }

    /// <inheritdoc cref="CarrierName"/>
    public string? CarrierCallSign { get; set; }

    /// <summary>
    /// The carrier as Elite writes it for display — <c>"Sacred Fire BNH-T2F"</c> — which is what the
    /// <c>From</c> field of its own traffic literally holds (#109).
    /// </summary>
    public string? CarrierDisplayName { get; set; }

    /// <summary>Channels carrying messages a person typed.</summary>
    private static bool IsAPlayerChannel(string channel) => VoiceGroups.IsAPerson(channel);

    /// <summary>
    /// A Frontier-canned line from the Commander's own carrier, on its way to the rewording brief
    /// rather than the verbatim reader (#248).
    /// </summary>
    public const string CarrierCannedKey = "carrier.comms";

    /// <summary>The same road for a System Authority vessel patrolling near the Commander's own carrier.</summary>
    public const string AuthorityCannedKey = "authority.comms";

    /// <summary>
    /// Whether the Commander currently shares a system with their own carrier (#248's second half,
    /// asked in the same chat): the condition under which a System Authority vessel's canned line is
    /// worth the owner treatment.
    /// </summary>
    public Func<bool> AuthorityNearOwnCarrier { get; set; } = () => false;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Never from the backlog.
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

    /// <summary>One message, or null if it is not one to speak.</summary>
    private bool IsMyCarrier(string sender) =>
        sender.Length > 0
        && ((CarrierCallSign is { Length: > 0 } call
             && sender.Contains(call, StringComparison.OrdinalIgnoreCase))
            || (CarrierName is { Length: > 0 } named
                && sender.Contains(named, StringComparison.OrdinalIgnoreCase))
            || (CarrierDisplayName is { Length: > 0 } shown
                && sender.Contains(shown, StringComparison.OrdinalIgnoreCase)));

    public Announcement? Read(JournalEvent journalEvent)
    {
        var channel = journalEvent.String("Channel") ?? "npc";
        var isPlayer = IsAPlayerChannel(channel);

        if (!isPlayer && !IncludeNpcs())
        {
            return null;
        }

        // Filtered here, before an announcement exists, so a channel switched off is never composed, voiced
        // or billed for — not filtered after the fact at the speaking path.
        if (isPlayer && !ChannelEnabled(channel))
        {
            return null;
        }

        // Elite tells you which channel you have joined every time you drop out of hyperspace, as a
        // ReceiveText from nobody.
        if (journalEvent.String("Message") is { } raw
            && raw.StartsWith("$COMMS_entered:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The localised form or nothing.
        var text = journalEvent.String("Message_Localised") ?? journalEvent.String("Message");

        if (text is null || Token().IsMatch(text))
        {
            return null;
        }

        // **Delivery direction is stripped here, at the boundary, and this is the only place it could be**
        // (#291).
        text = AudioTags.Strip(text);

        if (text.Length == 0)
        {
            return null;
        }

        var sender = Undecorate(
            journalEvent.String("From_Localised") ?? journalEvent.String("From") ?? "Unknown");

        // Elite echoes what you send back to you on the same channel.
        if (CommanderName is { Length: > 0 } own
            && sender.Contains(own, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // **A canned line from the Commander's own carrier is Frontier's string, not somebody else's words**
        // (#248).
        if (IsMyCarrier(sender)
            && journalEvent.String("Message") is { Length: > 1 } key
            && key[0] == '$'
            && journalEvent.String("Message_Localised") is { Length: > 0 })
        {
            return new Announcement(CarrierCannedKey, text)
            {
                Voice = VoiceRole.TowerControl,
                CommsChannel = channel,
                Transcript = $"{sender}: {text}\n",
                Cooldown = TimeSpan.Zero,
            };
        }

        // **And the patrol around it** (#248's second half).
        if (AuthorityNearOwnCarrier()
            && journalEvent.String("From") is { } authority
            && authority.StartsWith("$ShipName_Police", StringComparison.OrdinalIgnoreCase)
            && journalEvent.String("Message") is { Length: > 1 } policeKey
            && policeKey[0] == '$'
            && journalEvent.String("Message_Localised") is { Length: > 0 })
        {
            return new Announcement(AuthorityCannedKey, text)
            {
                Voice = VoiceRole.Comms,
                Speaker = sender,
                SpeakerIsPlayer = false,
                CommsChannel = channel,
                Transcript = $"{sender}: {text}\n",
                Cooldown = TimeSpan.Zero,
            };
        }

        return new Announcement($"message.{channel}", Spoken(sender, text, isPlayer))
        {
            // A carrier's traffic is its tower talking, and the Commander has cast a voice for it.
            Voice = IsMyCarrier(sender) ? VoiceRole.TowerControl : VoiceRole.Comms,
            Speaker = sender,
            SpeakerIsPlayer = isPlayer,

            // Which slot pays for this line, and it is not derivable from the boolean above: a squadron mate
            // and a stranger in local are both players (Phase 57).
            CommsChannel = channel,

            // Written down with the sender on it, whoever they are.
            Transcript = sender is { Length: > 0 } named
                ? $"{named}: {text}\n"
                : $"{text}\n",

            // No cooldown.
            Cooldown = TimeSpan.Zero,
        };
    }

    /// <summary>What is actually said out loud.</summary>
    private static string Spoken(string sender, string text, bool isPlayer) =>
        isPlayer && sender is { Length: > 0 } ? $"{sender} says: {text}" : text;

    /// <summary>
    /// Elite wraps names in localisation decorators: <c>$cmdr_decorate:#name=Vex;</c> and
    /// <c>$npc_name_decorate:#name=Ilse Bruhn;</c>.
    /// </summary>
    public static string Undecorate(string from)
    {
        var named = Decorated().Match(from);

        if (named.Success)
        {
            return named.Groups["name"].Value.Trim();
        }

        // A bare `$Something;` with no name inside it — a ship or faction id.
        return from.Trim('$', ';', ' ').Replace('_', ' ').Trim();
    }

    [GeneratedRegex(@"#name=(?<name>[^;]+);")]
    private static partial Regex Decorated();

    /// <summary>
    /// An unlocalised token: starts with <c>$</c> and ends with <c>;</c>, with no spaces that would
    /// suggest it is prose someone typed.
    /// </summary>
    [GeneratedRegex(@"^\$[^\s]*;?$")]
    private static partial Regex Token();
}
