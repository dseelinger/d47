using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// The invented people of the last overheard exchange, who answer when the Commander names them while
/// the exchange is still going. Thread-safe: <see cref="Heard"/> runs on the callout drain and
/// <see cref="RouteAsync"/> on the turn loop.
/// </summary>
/// <param name="clock">The time now.</param>
/// <param name="system">The Commander's current star system.</param>
/// <param name="situation">What the Commander is doing, as <see cref="AmbientLines.Situate"/> reads it.</param>
/// <param name="shipAiName">What the Commander calls the ship's AI; at the front of an utterance it closes the line.</param>
/// <param name="model">The model replies are written on, or null for the turn loop's own.</param>
/// <param name="accentOf">The accent a voice id speaks with, or null for none.</param>
/// <param name="facts">What the ship can prove about itself, which a reply may not contradict.</param>
public sealed class ChatterLine(
    Func<DateTimeOffset> clock,
    Func<string?> system,
    Func<AmbientSituation> situation,
    Func<string?> shipAiName,
    Func<string?>? model = null,
    Func<string?, string?>? accentOf = null,
    Func<ShipFacts>? facts = null,
    ILogger? logger = null) : ILine
{
    /// <summary>How long after the exchange's last line the Commander can still answer it.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(90);

    /// <summary>The most replies one exchange gives.</summary>
    public const int MostReplies = 4;

    private const string Commander = "Commander";

    private readonly Lock _lock = new();

    private Exchange? _exchange;

    /// <summary>The person the line is open to, or null.</summary>
    private string? _open;

    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                return _open is not null && _exchange is { } exchange && Carries(exchange);
            }
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            _open = null;
        }
    }

    /// <summary>
    /// Records a line as spoken. A new <paramref name="exchangeIndex"/> replaces the exchange held. Only
    /// invented speakers can be addressed; the tower and the captain are context.
    /// </summary>
    /// <param name="answerable">Whether the Commander may answer this exchange at all.</param>
    public void Heard(
        NpcChatterLine line,
        bool answerable,
        int exchangeIndex,
        string? system,
        AmbientSituation situation)
    {
        ArgumentNullException.ThrowIfNull(line);

        lock (_lock)
        {
            if (_exchange is not { } exchange || exchange.Index != exchangeIndex)
            {
                _exchange = exchange = new Exchange(exchangeIndex, answerable, system, situation);
                _open = null;
            }

            exchange.Said.Add($"{line.Name}: {line.Text}");
            exchange.LastLineAt = clock();

            if (line.Role is null && !exchange.People.ContainsKey(line.Name))
            {
                exchange.People[line.Name] = line.VoiceId;
            }
        }
    }

    /// <summary>Records a line as spoken in the Commander's current system and situation.</summary>
    public void Heard(NpcChatterLine line, bool answerable, int exchangeIndex) =>
        Heard(line, answerable, exchangeIndex, system(), situation());

    public Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(Route(input));
        }
    }

    private LineDecision Route(string input)
    {
        if (_exchange is not { Answerable: true } exchange || exchange.People.Count == 0)
        {
            _open = null;
            return new LineDecision.NotMine();
        }

        if (shipAiName() is { Length: > 0 } shipAi && CrewAddressing.Opens(input, shipAi) is not null)
        {
            _open = null;
            return new LineDecision.NotMine();
        }

        var carries = Carries(exchange);
        string name;
        string question;

        if (Named(input, exchange) is { } named)
        {
            if (!carries)
            {
                _open = null;
                return new LineDecision.Refused($"{named.Name} is off the channel.", TurnRoute.ChatterClosed);
            }

            (name, question) = named;
        }
        else if (_open is { } open && carries)
        {
            name = open;
            question = input.Trim();
        }
        else
        {
            _open = null;
            return new LineDecision.NotMine();
        }

        exchange.Replies++;

        _open = exchange.Replies >= MostReplies
                || CaptainLine.IsDismissal(input, name)
                || CaptainLine.IsDismissal(input, LastWord(name))
            ? null
            : name;

        if (question.Length > 0)
        {
            exchange.Said.Add($"{Commander}: {question}");
        }

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.Comms,
                name,
                Brief(name, exchange),
                [],
                OffersTools: false,
                Signal: 1,
                Model: model?.Invoke(),
                Screen: reply => Screened(reply, name, exchange)),
            question.Length == 0 ? "The Commander is trying to get your attention." : question);
    }

    /// <summary>The reply, or a fixed sign-off that shuts the exchange when the reply may not be said.</summary>
    private string Screened(string reply, string name, Exchange exchange)
    {
        var said = reply.Trim();

        var sayable = FlavourBriefs.MayBeSpoken(said)
                      && !NpcChatter.Escalates(said)
                      && ContradictedClaims.Sayable(said, facts?.Invoke() ?? ShipFacts.Unknown, logger, NpcChatter.KeyPrefix + "reply") is not null;

        lock (_lock)
        {
            if (!sayable)
            {
                said = $"Got to go. {name} out.";
                exchange.Shut = true;
            }

            exchange.Said.Add($"{name}: {said}");
            exchange.LastLineAt = clock();

            if (exchange.Shut && _exchange == exchange)
            {
                _open = null;
            }
        }

        return said;
    }

    /// <summary>Whether the Commander can still be answered in this exchange.</summary>
    private bool Carries(Exchange exchange) =>
        exchange.Answerable
        && !exchange.Shut
        && exchange.Replies < MostReplies
        && clock() - exchange.LastLineAt <= Window
        && string.Equals(system(), exchange.System, StringComparison.OrdinalIgnoreCase)
        && situation() == exchange.Situation;

    /// <summary>The held speaker the input opens with — whole name or its last word — and what follows.</summary>
    private static (string Name, string Question)? Named(string input, Exchange exchange)
    {
        foreach (var name in exchange.People.Keys.OrderByDescending(name => name.Length))
        {
            if (CrewAddressing.Opens(input, name) is { } rest)
            {
                return (name, rest);
            }
        }

        foreach (var name in exchange.People.Keys.OrderByDescending(name => name.Length))
        {
            if (LastWord(name) is var last && last != name && CrewAddressing.Opens(input, last) is { } rest)
            {
                return (name, rest);
            }
        }

        return null;
    }

    private static string LastWord(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } words ? words[^1] : name;

    private string Brief(string name, Exchange exchange)
    {
        var accent = SpeakerAccent.Sentence(accentOf?.Invoke(exchange.People[name]));
        var last = exchange.Replies >= MostReplies
            ? " This is your last reply: sign off in it."
            : string.Empty;

        return $"""
                 You are {name}, one of the people the Commander just overheard on the comms channel.
                 You are a human being, not an artificial intelligence, and you never claim to be one.
                 The Commander has answered you. What has been said so far:

                 {string.Join('\n', exchange.Said)}

                 Answer in one or two short spoken lines, as yourself: no narration, no stage directions,
                 no quotation marks. Never name or imitate a real person or another player.
                 You do not scan, interdict, target, fine or put a bounty on the Commander, grant or deny
                 them docking, give them or take their cargo, or send them a wing or friend invite.{(accent is null ? string.Empty : " " + accent)}{last}
                 """;
    }

    private sealed class Exchange(int index, bool answerable, string? system, AmbientSituation situation)
    {
        public int Index { get; } = index;

        public bool Answerable { get; } = answerable;

        public string? System { get; } = system;

        public AmbientSituation Situation { get; } = situation;

        /// <summary>Invented speakers by name, with the voice each was heard in.</summary>
        public Dictionary<string, string?> People { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every line so far, overheard and replied, as "Name: text".</summary>
        public List<string> Said { get; } = [];

        public DateTimeOffset LastLineAt { get; set; }

        public int Replies { get; set; }

        public bool Shut { get; set; }
    }
}
