using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// In-game chat, read aloud in somebody else's voice (Phase 11, "Speak incoming
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
    /// Whether one <em>player</em> channel is spoken, asked with the raw <c>Channel</c> value
    /// (<a href="https://github.com/dseelinger/d47/issues/299">#299</a>). Never asked for
    /// <c>npc</c>, which keeps its own switch above.
    /// <para>
    /// True for everything by default, so a Commander who never opens the five new rows hears
    /// exactly what they heard before this existed.
    /// </para>
    /// </summary>
    public Func<string, bool> ChannelEnabled { get; set; } = _ => true;

    /// <summary>
    /// The Commander's own name, so their own messages are not read back to them. Elite echoes
    /// what you send on the same channel it went out on.
    /// </summary>
    public string? CommanderName { get; set; }

    /// <summary>
    /// The Commander's <b>own</b> fleet carrier, so a message from it comes in the tower's voice
    /// rather than a stranger's (<a href="https://github.com/dseelinger/d47/issues/28">#28</a>).
    /// <para>
    /// <b>Both, and only from identified state.</b> The callsign is the stronger of the two:
    /// <c>CarrierState</c> only takes it from a docking whose MarketID is the carrier id it already
    /// holds, which is an ownership check rather than a name that happens to match. The name comes
    /// from <c>CarrierStats</c>, which one corpus account receives for two carrier ids in the same
    /// journal — so it is matched as well, never instead.
    /// </para>
    /// <para>
    /// A squadron's carrier must not reach this. The state behind it has been filtered on
    /// <c>CarrierType</c> since 0.45.0, for the same reason: Elite writes <c>CarrierLocation</c> for
    /// the Commander's own carrier and for a squadron's seconds apart, and 152 of the 173 corpus
    /// journals carrying both had the squadron's last.
    /// </para>
    /// </summary>
    public string? CarrierName { get; set; }

    /// <inheritdoc cref="CarrierName"/>
    public string? CarrierCallSign { get; set; }

    /// <summary>
    /// The carrier as Elite writes it for display — <c>"Sacred Fire BNH-T2F"</c> — which is what
    /// the <c>From</c> field of its own traffic literally holds
    /// (<a href="https://github.com/dseelinger/d47/issues/109">#109</a>).
    /// <para>
    /// <b>The third key, and the only one that is ever known in time.</b> The other two are learned
    /// at the airlock and from <c>CarrierStats</c>; docking chatter arrives before the airlock, so
    /// in the reported session all three messages were spoken in an NPC voice while both were still
    /// null. This one is learned from a supercruise drop whose MarketID is the carrier's id, twelve
    /// seconds ahead of the first line.
    /// </para>
    /// <para>
    /// <b>It is not a weaker check for being a longer string.</b> Like the other two it comes only
    /// from state that was vouched by id — a squadron carrier's drop carries a different MarketID
    /// and never reaches this field, which is the rule #28 was built on.
    /// </para>
    /// </summary>
    public string? CarrierDisplayName { get; set; }

    /// <summary>
    /// Channels carrying messages a person typed. <c>starsystem</c> is included because system
    /// chat is players; <c>npc</c> is the only channel that is not, which is why it is the one
    /// with its own switch.
    /// <para>
    /// The list itself lives in <see cref="VoiceGroups"/> as of Phase 57, which sorts the same
    /// channels into the slots that pay for them. Two copies of it would be two things to keep
    /// agreeing about <c>starsystem</c> — and a channel spoken by one and gated by the other is
    /// exactly the disagreement that would not report itself.
    /// </para>
    /// </summary>
    private static bool IsAPlayerChannel(string channel) => VoiceGroups.IsAPerson(channel);

    /// <summary>
    /// A Frontier-canned line from the Commander's own carrier, on its way to the rewording
    /// brief rather than the verbatim reader (#248). One key for all of them: two canned
    /// messages are two events, and the cooldown is zero for the reason every message's is.
    /// </summary>
    public const string CarrierCannedKey = "carrier.comms";

    /// <summary>The same road for a System Authority vessel patrolling near the Commander's own carrier.</summary>
    public const string AuthorityCannedKey = "authority.comms";

    /// <summary>
    /// Whether the Commander currently shares a system with their own carrier (#248's second
    /// half, asked in the same chat): the condition under which a System Authority vessel's
    /// canned line is worth the owner treatment. False until the app wires it, which keeps the
    /// replay harness and every existing test exactly as they were.
    /// </summary>
    public Func<bool> AuthorityNearOwnCarrier { get; set; } = () => false;

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
    /// <summary>
    /// Whether a sender is the Commander's own carrier (#28). Contains rather than equals, because
    /// Elite decorates a carrier's name with its callsign and the two arrive together.
    /// </summary>
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

        // Filtered here, before an announcement exists, so a channel switched off is never
        // composed, voiced or billed for — not filtered after the fact at the speaking path.
        if (isPlayer && !ChannelEnabled(channel))
        {
            return null;
        }

        // Elite tells you which channel you have joined every time you drop out of hyperspace,
        // as a ReceiveText from nobody. It is 8,833 events across the corpus and second in
        // volume only to station traffic, and it is a fact the Commander can read off the
        // system name in front of them. Matched on the unlocalised token rather than on the
        // English sentence, so a Commander playing in German is not read it either.
        if (journalEvent.String("Message") is { } raw
            && raw.StartsWith("$COMMS_entered:", StringComparison.OrdinalIgnoreCase))
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

        // **Delivery direction is stripped here, at the boundary, and this is the only place it
        // could be** (#291). A tag in square brackets is an instruction to the voice, and this
        // text was typed by another player: left alone, `[shouting]` or `[strong German accent]`
        // in a message would let a stranger decide how d47 sounds while reading their words out.
        //
        // At the point the untrusted text arrives rather than in the speaking path, because that
        // is where the rule belongs — the pipeline's job is to know which providers perform tags,
        // not which sentences are to be trusted. Everything downstream may then treat a tag as
        // something d47's own model wrote, which is the only way one ever gets there.
        text = AudioTags.Strip(text);

        if (text.Length == 0)
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

        // **A canned line from the Commander's own carrier is Frontier's string, not somebody
        // else's words** (#248). Elite writes canned traffic with a `$…;` key in Message and
        // player free text with none, so a $-keyed message from the own carrier is the same
        // trust class as a journal event — it may ride a rewording brief the way d47's own
        // authored lines do, which is what turns "docking granted—welcome back" boilerplate
        // into the tower talking to its owner. Free text from the same sender keeps the
        // verbatim road below, because a sender is a name and a name can be worn.
        //
        // The transcript keeps the original, on the Commander's instruction: the comms page
        // records what Elite actually sent, while the voice says the reworded line — the same
        // split the ship AI's own varied callouts already live with. The fallback with no model
        // is the localised line spoken exactly as it is today.
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

        // **And the patrol around it** (#248's second half). A System Authority vessel's canned
        // line, while the Commander shares a system with their own carrier, takes the same road
        // for the same reason — the $-keys on both fields prove Frontier wrote every word. The
        // raw From is matched rather than the localised one, so the treatment is the same in
        // any language. The Speaker stays, so the line keeps its pooled per-system voice.
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

            // Which slot pays for this line, and it is not derivable from the boolean above:
            // a squadron mate and a stranger in local are both players (Phase 57).
            CommsChannel = channel,

            // Written down with the sender on it, whoever they are. The page has no voices to
            // tell two senders apart with, and a wall of unattributed lines is worse than the
            // preamble ever was in the ear.
            Transcript = sender is { Length: > 0 } named
                ? $"{named}: {text}\n"
                : $"{text}\n",

            // No cooldown. Two messages from the same channel are two messages, not one warning
            // said twice — the cooldown exists for conditions that stay true across hundreds of
            // ticks, and a chat line is an event.
            Cooldown = TimeSpan.Zero,
        };
    }

    /// <summary>
    /// What is actually said out loud.
    /// <para>
    /// <b>An NPC is read as the words alone.</b> Elite's NPC traffic is stations, police
    /// interceptors, passenger liners and ships identified by class — in the corpus the commonest
    /// senders by far are <c>$ShipName_Police_Independent;</c> and a station's name, not people —
    /// so the preamble was reading out "ShipName Police Independent says" in front of a
    /// three-word transmission, several times on an approach. The sender is not lost: it is on
    /// the page, in <see cref="Announcement.Transcript"/>, and the voice carries who it is in the
    /// ear.
    /// </para>
    /// <para>
    /// <b>A player keeps theirs.</b> A Commander's name is a name, said once, and it is the thing
    /// the Commander most needs to know about a message that arrived in wing chat — the voice
    /// says "not you" but only the name says which of the three of them it was.
    /// </para>
    /// </summary>
    private static string Spoken(string sender, string text, bool isPlayer) =>
        isPlayer && sender is { Length: > 0 } ? $"{sender} says: {text}" : text;

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
