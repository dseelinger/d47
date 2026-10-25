using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// The strangers on the SYSTEM channel, by roster name: the invented people of overheard exchanges, and real comms
/// NPCs. Thread-safe: <see cref="Heard"/> runs on the callout drain and <see cref="RouteAsync"/> on the turn loop.
/// </summary>
/// <param name="system">The Commander's current star system.</param>
/// <param name="model">The model replies are written on, or null for the turn loop's own.</param>
/// <param name="accentOf">The accent an invented speaker's voice id speaks with, or null for none.</param>
/// <param name="facts">What the ship can prove about itself, which a reply may not contradict.</param>
/// <param name="roster">The NPCs heard here, which keep the personality written for each, or null for none.</param>
/// <param name="writePersonality">Writes one NPC's personality on the background model, or null for none.</param>
/// <param name="accentHeardAs">The accent of the voice an incoming message resolves to, or null for none.</param>
public sealed class ChatterLine(
    Func<string?> system,
    Func<string?>? model = null,
    Func<string?, string?>? accentOf = null,
    Func<ShipFacts>? facts = null,
    ILogger? logger = null,
    Func<NpcsHeardHere?>? roster = null,
    Func<NpcHeard, CancellationToken, Task<string?>>? writePersonality = null,
    Func<Announcement, string?>? accentHeardAs = null) : ILine
{
    private const string Commander = "Commander";

    private readonly Lock _lock = new();

    /// <summary>The exchange held last, which the next line of the same index joins.</summary>
    private Exchange? _last;

    /// <summary>The exchange each invented speaker was last heard in.</summary>
    private readonly Dictionary<string, Exchange> _heardIn = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When each invented speaker was last heard, as a count of lines heard.</summary>
    private readonly Dictionary<string, long> _heardAt = new(StringComparer.OrdinalIgnoreCase);

    private long _lines;

    /// <summary>NPCs whose personality could not be written, so the plain brief is used for the stay.</summary>
    private readonly HashSet<string> _noPersonality = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The invented speakers heard in the current system, most recently heard first.</summary>
    public IReadOnlyList<string> Speakers
    {
        get
        {
            lock (_lock)
            {
                var here = system();

                return [.. _heardIn
                    .Where(pair => In(pair.Value, here))
                    .OrderByDescending(pair => _heardAt.GetValueOrDefault(pair.Key))
                    .Select(pair => pair.Key)];
            }
        }
    }

    /// <summary>
    /// Records a line as spoken in the Commander's current system. A new <paramref name="exchangeIndex"/> starts a new
    /// exchange. Only invented speakers can be answered; the tower and the captain are context.
    /// </summary>
    public void Heard(NpcChatterLine line, int exchangeIndex)
    {
        ArgumentNullException.ThrowIfNull(line);

        lock (_lock)
        {
            var here = system();

            foreach (var gone in _heardIn.Where(pair => !In(pair.Value, here)).Select(pair => pair.Key).ToList())
            {
                _heardIn.Remove(gone);
                _heardAt.Remove(gone);
            }

            if (_last is not { } exchange || exchange.Index != exchangeIndex || !In(exchange, here))
            {
                _last = exchange = new Exchange(exchangeIndex, here);
            }

            exchange.Said.Add($"{line.Name}: {line.Text}");

            if (line.Role is null)
            {
                exchange.People[line.Name] = line.VoiceId;
                _heardIn[line.Name] = exchange;
                _heardAt[line.Name] = ++_lines;
            }
        }
    }

    public async Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selected);

        if (selected is not { Channel: CommsChannel.System, SubChannel.Key: var key })
        {
            return new LineDecision.NotMine();
        }

        var npc = roster?.Invoke()?.Find(key);

        if (npc is { Kind: NpcKind.Comms })
        {
            return await RouteCommsNpcAsync(npc, input, cancellationToken).ConfigureAwait(false);
        }

        Exchange? exchange;

        lock (_lock)
        {
            exchange = _heardIn.TryGetValue(key, out var heardIn) && In(heardIn, system()) ? heardIn : null;
        }

        // An invented speaker answers only from an exchange they spoke in; a carrier post heard in chatter is never one.
        if (exchange is null)
        {
            return new LineDecision.NotMine();
        }

        var name = npc?.Name ?? key;

        if (npc is not null)
        {
            await PersonalityAsync(npc, cancellationToken).ConfigureAwait(false);
        }

        var question = input.Trim();
        string brief;

        lock (_lock)
        {
            if (question.Length > 0)
            {
                exchange?.Said.Add($"{Commander}: {question}");
            }

            brief = Brief(name, exchange, npc?.VoiceId);
        }

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.Comms,
                name,
                brief,
                [],
                OffersTools: false,
                Signal: 1,
                Model: model?.Invoke(),
                Screen: reply => Screened(reply, name, exchange)),
            question.Length == 0 ? "The Commander is trying to get your attention." : question);
    }

    /// <summary>
    /// A real comms NPC answers from its family and personality, in the voice its message was heard in; what it said is
    /// never held or sent.
    /// </summary>
    private async Task<LineDecision> RouteCommsNpcAsync(NpcHeard npc, string input, CancellationToken cancellationToken)
    {
        var question = input.Trim();

        var personality = await PersonalityAsync(npc, cancellationToken).ConfigureAwait(false);

        var heardAs = npc.HeardAs;

        var accent = SpeakerAccent.Sentence(heardAs is null ? null : accentHeardAs?.Invoke(heardAs));

        var trait = personality is null ? string.Empty : "\nYour personality: " + personality;

        var brief = $"""
                     {CannedSpeakers.For(npc.Family ?? string.Empty)}
                     You are {npc.Name}, a real contact on the comms channel. The Commander is talking to you.
                     You are a human being, not an artificial intelligence, and you never claim to be one.{trait}
                     Answer in one or two short spoken lines, as yourself: no narration, no stage directions,
                     no quotation marks. Never name or imitate a real person or another player.
                     You do not scan, interdict, target, fine or put a bounty on the Commander, grant or deny
                     them docking, give them or take their cargo, or send them a wing or friend invite.{(accent is null ? string.Empty : " " + accent)}
                     {SigningOff(npc.Name)}
                     """;

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.Comms,
                npc.Name,
                brief,
                [],
                OffersTools: false,
                Signal: 1,
                Model: model?.Invoke(),
                Screen: reply => Screened(reply, npc.Name, exchange: null),
                HeardAs: heardAs),
            question.Length == 0 ? "The Commander is trying to get your attention." : question);
    }

    /// <summary>The NPC's personality, written once for the stay unless it is already held or could not be written.</summary>
    private async Task<string?> PersonalityAsync(NpcHeard npc, CancellationToken cancellationToken)
    {
        if (npc.Personality is { } held)
        {
            return held;
        }

        if (writePersonality is null || roster?.Invoke() is not { } here)
        {
            return null;
        }

        lock (_lock)
        {
            if (_noPersonality.Contains(npc.Name))
            {
                return null;
            }
        }

        var written = await StrangerPersonality.WriteAsync(npc, writePersonality, logger, cancellationToken).ConfigureAwait(false);

        if (written is null)
        {
            lock (_lock)
            {
                _noPersonality.Add(npc.Name);
            }

            return null;
        }

        here.SetPersonality(npc.Name, written);
        return written;
    }

    /// <summary>The reply, or a fixed sign-off when it may not be said. A reply that signs off takes the speaker off the roster.</summary>
    private string Screened(string reply, string name, Exchange? exchange)
    {
        var said = reply.Trim();

        var sayable = FlavourBriefs.MayBeSpoken(said)
                      && !NpcChatter.Escalates(said)
                      && ContradictedClaims.Sayable(said, facts?.Invoke() ?? ShipFacts.Unknown, logger, NpcChatter.KeyPrefix + "reply") is not null;

        if (!sayable)
        {
            said = NpcsHeardHere.SignOff(name);
        }

        var left = NpcsHeardHere.SignsOff(said, name);

        lock (_lock)
        {
            exchange?.Said.Add($"{name}: {said}");

            if (left)
            {
                _heardIn.Remove(name);
                _heardAt.Remove(name);
            }
        }

        if (left)
        {
            roster?.Invoke()?.Left(name);
        }

        return said;
    }

    /// <summary>How a stranger ends the conversation, in the words <see cref="NpcsHeardHere.SignsOff"/> reads.</summary>
    private static string SigningOff(string name) =>
        $"When you are done talking, end your reply with exactly: {NpcsHeardHere.SignOff(name)}";

    private static bool In(Exchange exchange, string? system) =>
        string.Equals(exchange.System, system, StringComparison.OrdinalIgnoreCase);

    private string Brief(string name, Exchange? exchange, string? rosterVoice)
    {
        var voiceId = exchange is not null && exchange.People.TryGetValue(name, out var heardIn) ? heardIn : rosterVoice;
        var accent = SpeakerAccent.Sentence(accentOf?.Invoke(voiceId));
        var personality = roster?.Invoke()?.Find(name)?.Personality is { } trait ? $"\nYour personality: {trait}" : string.Empty;
        var said = exchange is null ? string.Empty : $"\nWhat has been said so far:\n\n{ExchangeLines.Recent(exchange.Said)}\n";

        return $"""
                 You are {name}, one of the people the Commander overheard on the comms channel in this system.
                 You are a human being, not an artificial intelligence, and you never claim to be one.{personality}
                 The Commander is talking to you.{said}
                 Answer in one or two short spoken lines, as yourself: no narration, no stage directions,
                 no quotation marks. Never name or imitate a real person or another player.
                 You do not scan, interdict, target, fine or put a bounty on the Commander, grant or deny
                 them docking, give them or take their cargo, or send them a wing or friend invite.{(accent is null ? string.Empty : " " + accent)}
                 {SigningOff(name)}
                 """;
    }

    private sealed class Exchange(int index, string? system)
    {
        public int Index { get; } = index;

        public string? System { get; } = system;

        /// <summary>Invented speakers by name, with the voice each was heard in.</summary>
        public Dictionary<string, string?> People { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every line so far, overheard and replied, as "Name: text".</summary>
        public List<string> Said { get; } = [];
    }
}
