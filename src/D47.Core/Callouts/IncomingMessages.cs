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
    public const string CalloutId = "messages";

    public string Id => CalloutId;

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
    /// Any other NPC's Frontier-canned line, followed by the family its <c>$</c>-key names (#135). Starts
    /// with <c>message.</c>, so the session record still writes it down.
    /// </summary>
    public const string CannedKeyPrefix = "message.canned.";

    /// <summary>
    /// The family a canned key names: the text between the leading <c>$</c> and the first <c>_</c>,
    /// <c>;</c> or <c>:</c>, with trailing digits dropped — <c>$Pirate_Scan01;</c> is <c>Pirate</c>,
    /// <c>$MinerCriticalDamage04;</c> is <c>MinerCriticalDamage</c>.
    /// </summary>
    public static string FamilyOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var body = key.StartsWith('$') ? key[1..] : key;
        var end = body.IndexOfAny(['_', ';', ':']);

        return (end < 0 ? body : body[..end]).TrimEnd("0123456789".ToCharArray());
    }

    /// <summary>
    /// Whether the Commander currently shares a system with their own carrier (#248's second half,
    /// asked in the same chat): the condition under which a System Authority vessel's canned line is
    /// worth the owner treatment.
    /// </summary>
    public Func<bool> AuthorityNearOwnCarrier { get; set; } = () => false;

    /// <summary>The station the Commander is docked at right now, or null (#68).</summary>
    public Func<string?> DockedStationName { get; set; } = () => null;

    /// <summary>That station's <c>StationAllegiance</c>, read live alongside <see cref="DockedStationName"/>.</summary>
    public Func<string?> DockedStationAllegiance { get; set; } = () => null;

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

    private readonly Dictionary<string, int> lastNotice = new(StringComparer.Ordinal);

    public Announcement? Read(JournalEvent journalEvent) =>
        ReadCore(journalEvent) is { } read ? InOwnWords(read, journalEvent.Timestamp) : null;

    /// <summary>A no-fire zone notice is said from d47's own pool, never Elite's wording.</summary>
    private Announcement InOwnWords(Announcement read, DateTimeOffset at)
    {
        if (StationNotices.Normalise(read.MessageKey) is not { } key)
        {
            return read;
        }

        var pool = StationNotices.PoolFor(key);
        int index;

        lock (lastNotice)
        {
            index = StationNotices.Pick(at, pool.Count, lastNotice.TryGetValue(key, out var previous) ? previous : null);
            lastNotice[key] = index;
        }

        var address = read.Key == CarrierCannedKey
            ? CommanderAddress.Said(CommanderName)
            : StationNotices.StationAddress;

        return read with { Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, pool[index], address) };
    }

    private Announcement? ReadCore(JournalEvent journalEvent)
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
        var frontiers = JournalText.IsFrontiersString(journalEvent.Raw);

        if (IsMyCarrier(sender) && frontiers)
        {
            return new Announcement(CarrierCannedKey, text)
            {
                Voice = VoiceRole.TowerControl,
                CommsChannel = channel,
                Origin = isPlayer ? AnnouncementOrigin.None : AnnouncementOrigin.Comms,
                Transcript = $"{sender}: {text}\n",
                Cooldown = TimeSpan.Zero,
                MessageKey = journalEvent.String("Message"),
            };
        }

        // **And the patrol around it** (#248's second half).
        if (AuthorityNearOwnCarrier()
            && journalEvent.String("From") is { } authority
            && authority.StartsWith("$ShipName_Police", StringComparison.OrdinalIgnoreCase)
            && frontiers)
        {
            return VoicedAs(sender, VoiceRole.Comms, channel, isPlayer: false, allegiance: null) with
            {
                Key = AuthorityCannedKey,
                Text = text,
                Origin = AnnouncementOrigin.Comms,
                Transcript = $"{sender}: {text}\n",
                Cooldown = TimeSpan.Zero,
            };
        }

        // **Any other NPC's canned line goes to the brief for its family** (#135). A player channel never does.
        var key = !isPlayer && frontiers
            ? CannedKeyPrefix + FamilyOf(journalEvent.String("Message")!)
            : $"message.{channel}";

        return VoicedAs(
            sender,

            // A carrier's traffic is its tower talking, and the Commander has cast a voice for it.
            IsMyCarrier(sender) ? VoiceRole.TowerControl : VoiceRole.Comms,
            channel,
            isPlayer,

            // Only the station the Commander is actually docked at names an allegiance — a stranger on
            // the open channel is not the local controller (#68).
            !isPlayer
                && DockedStationName() is { Length: > 0 } docked
                && string.Equals(docked, sender, StringComparison.OrdinalIgnoreCase)
                ? DockedStationAllegiance()
                : null) with
        {
            Key = key,
            Text = Spoken(sender, text, isPlayer),
            Origin = isPlayer ? AnnouncementOrigin.None : AnnouncementOrigin.Comms,

            // Written down with the sender on it, whoever they are.
            Transcript = sender is { Length: > 0 } named
                ? $"{named}: {text}\n"
                : $"{text}\n",

            // No cooldown.
            Cooldown = TimeSpan.Zero,
            MessageKey = frontiers ? journalEvent.String("Message") : null,
        };
    }

    /// <summary>
    /// A message from <paramref name="sender"/> with only the fields that decide its voice set. Every incoming message
    /// with a sender is built from this, and so is the reply of someone the Commander talks to on comms.
    /// </summary>
    /// <param name="channel">
    /// Which slot pays for the line, which <paramref name="isPlayer"/> does not decide: a squadron mate and a stranger
    /// in local are both players (Phase 57).
    /// </param>
    public static Announcement VoicedAs(string sender, VoiceRole voice, string channel, bool isPlayer, string? allegiance) =>
        new($"message.{channel}", string.Empty)
        {
            Voice = voice,
            Speaker = sender,
            SpeakerIsPlayer = isPlayer,
            SpeakerAllegiance = allegiance,
            CommsChannel = channel,
        };

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
