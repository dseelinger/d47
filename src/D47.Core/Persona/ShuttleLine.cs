using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// The pilot of the Apex or Frontline shuttle the Commander is riding, who answers whatever is said while the SHUTTLE
/// comms channel is selected. The transcript and personality last for one ride. Thread-safe.
/// </summary>
/// <param name="ride">The ride in progress, or null.</param>
/// <param name="currentSystem">The system the Commander is in, or null.</param>
/// <param name="model">The model replies are written on, or null for the turn loop's own.</param>
/// <param name="writePersonality">Writes the pilot's personality on the background model, or null for none.</param>
/// <param name="accentHeardAs">The accent of the voice an incoming message resolves to, or null for none.</param>
public sealed class ShuttleLine(
    Func<ShuttleRide?> ride,
    Func<string?> currentSystem,
    Func<string?>? model = null,
    ILogger? logger = null,
    Func<NpcHeard, CancellationToken, Task<string?>>? writePersonality = null,
    Func<Announcement, string?>? accentHeardAs = null) : ILine
{
    private readonly Lock _lock = new();

    private ShuttleRide? _heldRide;

    private List<ConversationMessage> _conversation = [];

    private string? _personality;

    private bool _personalityWritten;

    public async Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var current = ride();

        Hold(current);

        if (selected is not { Channel: CommsChannel.Shuttle, SubChannel.Key: CommsChannels.PilotKey } || current is null)
        {
            return new LineDecision.NotMine();
        }

        var question = input.Trim();

        if (question.Length == 0)
        {
            question = "The Commander is trying to get your attention.";
        }

        var name = CommsChannels.PilotName(current.Kind);
        var personality = await PersonalityAsync(current, name, cancellationToken).ConfigureAwait(false);
        var heardAs = IncomingMessages.VoicedAs(IncomingMessages.Undecorate(name), VoiceRole.Comms, "npc", isPlayer: false, allegiance: null);

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.Comms,
                name,
                Brief(name, current, personality, SpeakerAccent.Sentence(accentHeardAs?.Invoke(heardAs))),
                Conversation(),
                OffersTools: false,
                Signal: 1,
                Model: model?.Invoke(),
                Screen: reply => Screened(reply, name),
                LiveState: () => Knows(current, currentSystem()),
                HeardAs: heardAs),
            question);
    }

    internal static string Knows(ShuttleRide ride, string? system)
    {
        var lines = new List<string>();

        if (ride.DestinationSystem is { Length: > 0 } destination)
        {
            lines.Add($"Destination system: {destination}.");
        }

        if (ride.DestinationLocation is { Length: > 0 } place)
        {
            lines.Add($"Destination: {place}.");
        }

        if (system is { Length: > 0 })
        {
            lines.Add($"Current system: {system}.");
        }

        return lines.Count == 0 ? "The route is not known." : string.Join(' ', lines);
    }

    internal static string Brief(string name, ShuttleRide ride, string? personality, string? accent)
    {
        var job = ride.Kind switch
        {
            ShuttleKind.Apex =>
                "a civilian pilot employed by Apex Interstellar, flying the Commander as a paying passenger to the booked destination.",
            ShuttleKind.Frontline => ride.Retreat
                ? "a Frontline Solutions dropship pilot, flying the Commander out of a ground conflict zone on a retreat."
                : "a Frontline Solutions dropship pilot, flying the Commander into a ground conflict zone.",
            _ => "the pilot of a shuttle flying the Commander to the booked destination.",
        };

        return $"""
                You are {name}, {job}
                You are a human being, not an artificial intelligence, and you never claim to be one.{(personality is null ? string.Empty : $"\nYour personality: {personality}")}
                You know the route, the destination and the system you are in, and nothing of the Commander's ships,
                missions or the galaxy beyond that. Invent no figures: no times, distances, prices or counts.
                You do not speak for the ship's core.
                Answer in one or two short spoken lines, as yourself: no narration, no stage directions,
                no quotation marks. Never name or imitate a real person or another player.
                You do not scan, target, fine or put a bounty on the Commander, give them or take their cargo,
                or send them a wing or friend invite.{(accent is null ? string.Empty : " " + accent)}
                """;
    }

    /// <summary>Starts a new transcript and personality when the ride is not the one held.</summary>
    private void Hold(ShuttleRide? current)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(_heldRide, current))
            {
                _heldRide = current;
                _conversation = [];
                _personality = null;
                _personalityWritten = false;
            }
        }
    }

    private List<ConversationMessage> Conversation()
    {
        lock (_lock)
        {
            if (_conversation.Count > ExchangeLines.Most)
            {
                _conversation.RemoveRange(0, _conversation.Count - ExchangeLines.Most);
            }

            return _conversation;
        }
    }

    private async Task<string?> PersonalityAsync(ShuttleRide current, string name, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_personalityWritten)
            {
                return _personality;
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
            if (ReferenceEquals(_heldRide, current))
            {
                _personality = written;
                _personalityWritten = true;
            }
        }

        return written;
    }

    private static string Screened(string reply, string name)
    {
        var said = reply.Trim();

        var sayable = FlavourBriefs.MayBeSpoken(said) && !NpcChatter.Escalates(said);

        return sayable ? said : NpcsHeardHere.SignOff(name);
    }
}
