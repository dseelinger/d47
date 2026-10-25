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
public sealed class TargetLine(
    Func<TargetedShip?> target,
    Func<string?>? model = null,
    Func<ShipFacts>? facts = null,
    ILogger? logger = null,
    Func<NpcHeard, CancellationToken, Task<string?>>? writePersonality = null,
    Func<NpcsHeardHere?>? roster = null,
    Func<Announcement, string?>? accentHeardAs = null) : ILine
{
    private const string KeyPrefix = "target.reply";

    private readonly Lock _lock = new();

    private readonly Dictionary<string, string?> _personalities = new(StringComparer.OrdinalIgnoreCase);

    public async Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selected);

        if (selected is not { Channel: CommsChannel.Target, SubChannel: not null }
            || target() is not { IsCommander: false, PilotName: { Length: > 0 } name } ship)
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

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.Comms,
                name,
                Brief(name, ship, personality, SpeakerAccent.Sentence(accentHeardAs?.Invoke(heardAs))),
                [],
                OffersTools: false,
                Signal: 1,
                Model: model?.Invoke(),
                Screen: reply => Screened(reply, name),
                HeardAs: heardAs),
            question);
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

    private static string Brief(string name, TargetedShip ship, string? personality, string? accent)
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

        return $"""
                You are {name}, the pilot of the ship the Commander has just targeted and hailed. {string.Join(' ', facts)}
                You are a human being, not an artificial intelligence, and you never claim to be one.{(personality is null ? string.Empty : $"\nYour personality: {personality}")}
                Answer in one or two short spoken lines, as yourself: no narration, no stage directions,
                no quotation marks. Never name or imitate a real person or another player.
                You do not scan, interdict, target, fine or put a bounty on the Commander, grant or deny
                them docking, give them or take their cargo, or send them a wing or friend invite.{(accent is null ? string.Empty : " " + accent)}
                """;
    }

    private string Screened(string reply, string name)
    {
        var said = reply.Trim();

        var sayable = FlavourBriefs.MayBeSpoken(said)
                      && !NpcChatter.Escalates(said)
                      && ContradictedClaims.Sayable(said, facts?.Invoke() ?? ShipFacts.Unknown, logger, KeyPrefix) is not null;

        return sayable ? said : NpcsHeardHere.SignOff(name);
    }
}
