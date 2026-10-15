using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// A running story's cast member, who answers whatever the Commander says shortly after that member's line, in the
/// first person. A command with a model-free route is left to the ship.
/// Thread-safe: <see cref="Heard"/> runs on the callout drain and <see cref="RouteAsync"/> on the turn loop.
/// </summary>
/// <param name="modelFree">Whether an utterance has a route that needs no model.</param>
/// <param name="member">
/// The running story's cast member with this id, as the Commander meets them; null when there is none, or the story
/// is paused, switched off or without Odyssey.
/// </param>
/// <param name="hiddenStory">The running story's hidden layer as every speaker reads it, or null.</param>
public sealed class CastLine(
    Func<DateTimeOffset> clock,
    Func<string, bool> modelFree,
    Func<string, AdventureSpeaker?> member,
    Func<string?> hiddenStory,
    Func<ShipFacts>? facts = null,
    ILogger? logger = null) : ILine
{
    /// <summary>How long after the member's last line the Commander can still answer it.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(90);

    /// <summary>The most replies one line of the member's gives.</summary>
    public const int MostReplies = 4;

    private const string KeyPrefix = "cast.reply";

    private readonly Lock _lock = new();

    /// <summary>The member's line and the replies so far, as "Name: text" and "Commander: text".</summary>
    private readonly List<string> _said = [];

    private string? _castId;

    private string _name = string.Empty;

    private DateTimeOffset? _lastLineAt;

    private int _replies;

    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                return Carries() is not null;
            }
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            _lastLineAt = null;
        }
    }

    /// <summary>Records a line the cast member <paramref name="castId"/> spoke, which opens the line to them afresh.</summary>
    public void Heard(string castId, string line)
    {
        if (string.IsNullOrWhiteSpace(castId) || string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_lock)
        {
            if (member(castId) is not { } speaking)
            {
                _lastLineAt = null;
                return;
            }

            _castId = castId;
            _name = speaking.Name;
            _said.Clear();
            _said.Add($"{speaking.Name}: {line.Trim()}");
            _replies = 0;
            _lastLineAt = clock();
        }
    }

    public Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(Route(input));
        }
    }

    private LineDecision Route(string input)
    {
        if (Carries() is not { } speaking)
        {
            return new LineDecision.NotMine();
        }

        if (modelFree(input))
        {
            return new LineDecision.NotMine();
        }

        var said = input.Trim();

        if (said.Length == 0)
        {
            return new LineDecision.NotMine();
        }

        _replies++;
        _said.Add($"Commander: {said}");

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.Crew,
                speaking.Name,
                Brief(speaking),
                [],
                OffersTools: false,
                Signal: 1,
                Screen: Screened,
                HiddenStory: hiddenStory,
                Cast: speaking.Id),
            said);
    }

    /// <summary>The reply, or nothing when it may not be said, which also closes the line.</summary>
    private string Screened(string reply)
    {
        var said = reply.Trim();

        var sayable = FlavourBriefs.MayBeSpoken(said)
                      && ContradictedClaims.Sayable(said, facts?.Invoke() ?? ShipFacts.Unknown, logger, KeyPrefix) is not null;

        lock (_lock)
        {
            if (!sayable)
            {
                _lastLineAt = null;
                return string.Empty;
            }

            _said.Add($"{_name}: {said}");
            _lastLineAt = clock();
        }

        return said;
    }

    /// <summary>The Story log body of one reply: what the Commander said, then the answer.</summary>
    public static string Logged(string asked, string answer) => $"You: {asked.Trim()}\n\n{answer.Trim()}";

    /// <summary>The member the line is open to, or null when it is shut.</summary>
    private AdventureSpeaker? Carries() =>
        _castId is { } castId
        && _lastLineAt is { } last
        && _replies < MostReplies
        && clock() - last <= Window
            ? member(castId)
            : null;

    private string Brief(AdventureSpeaker speaking)
    {
        var last = _replies >= MostReplies ? " This is your last reply: bring the exchange to a close." : string.Empty;

        return $"""
                 You are {speaking.Name}, a person in the story the Commander is playing. {speaking.Who}
                 The Commander has answered you. Reply as {speaking.Name}, in the first person, speaking to the Commander,
                 in one to three short sentences, as said aloud. You are not the core and never speak for it. You
                 have no tools and change nothing aboard. The exchange so far:

                 {string.Join('\n', _said)}{last}
                 """;
    }
}
