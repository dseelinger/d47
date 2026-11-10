using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// The NPC pilot of the held target, who answers whatever the Commander says while the TARGET comms channel is
/// selected. A Commander target is never offered by the channel and never answers. Thread-safe.
/// </summary>
/// <param name="target">The held target, or null.</param>
/// <param name="model">The model replies are written on, or null for the turn loop's own.</param>
/// <param name="facts">What the ship can prove about itself, which a reply may not contradict.</param>
/// <param name="writePersonality">Writes one NPC's personality on the background model, or null for none.</param>
/// <param name="roster">The NPCs heard here, whose message voices a target heard on comms, or null for none.</param>
/// <param name="accentHeardAs">The accent of the voice an incoming message resolves to, or null for none.</param>
/// <param name="system">The Commander's current star system, or null.</param>
/// <param name="station">The station the Commander is docked at, or null.</param>
/// <param name="now">The wall clock, which dates the last scan; null leaves the age out.</param>
/// <param name="fightOn">Whether a fight is on around the Commander; null is never.</param>
public sealed class TargetLine(
    Func<TargetedShip?> target,
    Func<string?>? model = null,
    Func<ShipFacts>? facts = null,
    ILogger? logger = null,
    Func<NpcHeard, CancellationToken, Task<string?>>? writePersonality = null,
    Func<NpcsHeardHere?>? roster = null,
    Func<Announcement, string?>? accentHeardAs = null,
    Func<string?>? system = null,
    Func<string?>? station = null,
    Func<DateTimeOffset>? now = null,
    Func<bool>? fightOn = null) : ILine
{
    private const string KeyPrefix = "target.reply";

    private readonly Lock _lock = new();

    private string? _conversationWith;

    private List<ConversationMessage> _conversation = [];

    private readonly Dictionary<string, string?> _personalities = new(StringComparer.OrdinalIgnoreCase);

    public async Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var held = target();

        if (held is not { IsCommander: false, PilotName: { Length: > 0 } })
        {
            lock (_lock)
            {
                _conversationWith = null;
                _conversation = [];
            }
        }

        if (selected is not { Channel: CommsChannel.Target, SubChannel: not null }
            || held is not { IsCommander: false, PilotName: { Length: > 0 } name } ship)
        {
            return new LineDecision.NotMine();
        }

        var question = input.Trim();

        if (question.Length == 0)
        {
            question = "The Commander is trying to get your attention.";
        }

        var personality = await PersonalityAsync(name, cancellationToken).ConfigureAwait(false);

        var heardAs = HeardAs(name);

        var fighting = fightOn?.Invoke() ?? false;

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.Comms,
                name,
                Brief(name, ship, personality, SpeakerAccent.Sentence(accentHeardAs?.Invoke(heardAs)), now?.Invoke(), fighting),
                Conversation(name),
                OffersTools: false,
                Signal: 1,
                Model: model?.Invoke(),
                Screen: reply => Screened(reply, name, fighting),
                LiveState: () => OffShipKnowledge.Describe(system?.Invoke(), station?.Invoke()),
                HeardAs: heardAs),
            question);
    }

    /// <summary>The conversation held with the targeted pilot, dropped when another pilot or no one is targeted.</summary>
    private List<ConversationMessage> Conversation(string name)
    {
        lock (_lock)
        {
            if (!string.Equals(_conversationWith, name, StringComparison.OrdinalIgnoreCase))
            {
                _conversationWith = name;
                _conversation = [];
            }

            if (_conversation.Count > ExchangeLines.Most)
            {
                _conversation.RemoveRange(0, _conversation.Count - ExchangeLines.Most);
            }

            return _conversation;
        }
    }

    /// <summary>
    /// The message the pilot was heard in on comms, or the one a <c>ReceiveText</c> from them on the npc channel would
    /// be. A ship is never the docked station, the only sender given an allegiance.
    /// </summary>
    private Announcement HeardAs(string name)
    {
        var sender = IncomingMessages.Undecorate(name);

        return roster?.Invoke()?.Find(sender)?.HeardAs
            ?? IncomingMessages.VoicedAs(sender, VoiceRole.Comms, "npc", isPlayer: false, allegiance: null);
    }

    private async Task<string?> PersonalityAsync(string name, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_personalities.TryGetValue(name, out var held))
            {
                return held;
            }
        }

        string? written = null;

        if (writePersonality is not null)
        {
            var npc = new NpcHeard(name, NpcKind.Comms, null, null, DateTimeOffset.MinValue);
            written = await StrangerPersonality.WriteAsync(npc, writePersonality, logger, cancellationToken).ConfigureAwait(false);
        }

        lock (_lock)
        {
            _personalities[name] = written;
        }

        return written;
    }

    private static string Brief(string name, TargetedShip ship, string? personality, string? accent, DateTimeOffset? now, bool fighting)
    {
        var facts = new List<string>();

        if (ship.Ship is { Length: > 0 } hull)
        {
            facts.Add($"You fly a {hull}.");
        }

        if (ship.LegalStatus is { Length: > 0 } status)
        {
            facts.Add($"Your legal status is {status}.");
        }

        if (ship.Rank is { Length: > 0 } rank)
        {
            facts.Add($"Your combat rank is {rank}.");
        }

        if (ship.Faction is { Length: > 0 } faction)
        {
            facts.Add($"You fly for {faction}.");
        }

        if (ship.Bounty is { } bounty)
        {
            facts.Add($"There is a bounty of {bounty:N0} credits on you.");
        }

        if (LastScan(ship, now) is { } scan)
        {
            facts.Add(scan);
        }

        return $"""
                You are {name}, the pilot of the ship the Commander has just targeted and hailed. {string.Join(' ', facts)}
                You are a human being, not an artificial intelligence, and you never claim to be one.{(personality is null ? string.Empty : $"\nYour personality: {personality}")}
                Answer in one or two short spoken lines, as yourself: no narration, no stage directions,
                no quotation marks. Never name or imitate a real person or another player.
                {(fighting ? FightRules : PeaceRules)}{(accent is null ? string.Empty : " " + accent)}
                """;
    }

    private const string PeaceRules = """
        You do not scan, interdict, target, fine or put a bounty on the Commander, grant or deny
        them docking, give them or take their cargo, or send them a wing or friend invite.
        """;

    private const string FightRules = """
        There is a fight going on around you. You may threaten the Commander and trade insults over it.
        You do not scan, interdict, fine or put a bounty on the Commander, grant or deny
        them docking, give them or take their cargo, or send them a wing or friend invite.
        """;

    private static string? LastScan(TargetedShip ship, DateTimeOffset? now)
    {
        if (ship.ShieldHealth is null && ship.HullHealth is null)
        {
            return null;
        }

        var when = now is { } clock && ship.ScannedAt is { } at ? $", {Age(clock - at)} ago," : string.Empty;

        var held = new List<string>();

        if (ship.ShieldHealth is { } shields)
        {
            held.Add($"your shields were at {shields}%");
        }

        if (ship.HullHealth is { } hull)
        {
            held.Add($"your hull at {hull}%");
        }

        var text = $"When the Commander last scanned you{when} {string.Join(" and ", held)}";

        if (ship.Subsystem is { } subsystem)
        {
            text += ship.SubsystemHealth is { } health
                ? $", and their sights were on your {subsystem}, at {health}%"
                : $", and their sights were on your {subsystem}";
        }

        text += ".";

        if (ship.ShieldHealth is not null)
        {
            text += " Shields recharge, so yours may have changed since";
            text += ship.HullHealth is not null ? "; your hull is no better than that now." : ".";
        }
        else
        {
            text += " Your hull is no better than that now.";
        }

        return text;
    }

    private static string Age(TimeSpan age)
    {
        var seconds = (int)Math.Max(0, age.TotalSeconds);

        if (seconds < 60)
        {
            return seconds == 1 ? "1 second" : $"{seconds} seconds";
        }

        var minutes = seconds / 60;

        return minutes == 1 ? "1 minute" : $"{minutes} minutes";
    }

    private string Screened(string reply, string name, bool fighting)
    {
        var said = reply.Trim();

        var sayable = FlavourBriefs.MayBeSpoken(said)
                      && !(fighting ? NpcChatter.EscalatesBeyondAFight(said) : NpcChatter.Escalates(said))
                      && ContradictedClaims.Sayable(said, facts?.Invoke() ?? ShipFacts.Unknown, logger, KeyPrefix) is not null;

        return sayable ? said : NpcsHeardHere.SignOff(name);
    }
}
