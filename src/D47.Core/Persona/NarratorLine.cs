using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// The Narrator, who answers whatever the Commander says shortly after a narration with more narration. A
/// command with a model-free route, or an utterance opening with the ship AI's name, is left to the ship.
/// Thread-safe: <see cref="Heard"/> runs on the callout drain and <see cref="RouteAsync"/> on the turn loop.
/// </summary>
/// <param name="modelFree">Whether an utterance has a route that needs no model.</param>
/// <param name="hiddenStory">The running story's hidden layer as the Narrator reads it, or null.</param>
/// <param name="commander">How the Narrator names the Commander when there is no character sheet, or null.</param>
public sealed partial class NarratorLine(
    Func<DateTimeOffset> clock,
    Func<string?> shipAiName,
    Func<string, bool> modelFree,
    Func<string?> hiddenStory,
    Func<string?>? commander = null,
    Func<string?>? model = null,
    Func<ShipFacts>? facts = null,
    ILogger? logger = null) : ILine
{
    /// <summary>How long after the Narrator's last line the Commander can still answer it.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(90);

    /// <summary>The most replies one narration gives.</summary>
    public const int MostReplies = 4;

    public const string Name = "Narrator";

    private const string KeyPrefix = "narrator.reply";

    private readonly Lock _lock = new();

    /// <summary>The narration and its replies so far, as "Narrator: text" and "Commander: text".</summary>
    private readonly List<string> _said = [];

    private DateTimeOffset? _lastLineAt;

    private int _replies;

    /// <summary>Called with each reply the Narrator says.</summary>
    public Action<string>? Said { get; set; }

    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                return Carries();
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

    /// <summary>Records a narration as spoken, which opens the line afresh.</summary>
    public void Heard(string narration)
    {
        if (string.IsNullOrWhiteSpace(narration))
        {
            return;
        }

        lock (_lock)
        {
            _said.Clear();
            _said.Add($"{Name}: {narration.Trim()}");
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
        if (!Carries())
        {
            return new LineDecision.NotMine();
        }

        if (shipAiName() is { Length: > 0 } shipAi && CrewAddressing.Opens(input, shipAi) is not null)
        {
            _lastLineAt = null;
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
                VoiceRole.Narrator,
                Name,
                Brief(),
                [],
                OffersTools: false,
                Signal: 1,
                Model: model?.Invoke(),
                Screen: Screened,
                HiddenStory: hiddenStory),
            said);
    }

    /// <summary>The reply, or nothing when it may not be said or speaks to the Commander, which also closes the line.</summary>
    private string Screened(string reply)
    {
        var said = reply.Trim();

        var sayable = FlavourBriefs.MayBeSpoken(said)
                      && !AddressesTheCommander(said)
                      && ContradictedClaims.Sayable(said, facts?.Invoke() ?? ShipFacts.Unknown, logger, KeyPrefix) is not null;

        lock (_lock)
        {
            if (!sayable)
            {
                _lastLineAt = null;
                return string.Empty;
            }

            _said.Add($"{Name}: {said}");
            _lastLineAt = clock();
        }

        Said?.Invoke(said);

        return said;
    }

    /// <summary>Whether a narrated reply speaks to the Commander in the second person.</summary>
    public static bool AddressesTheCommander(string reply) => SecondPerson().IsMatch(reply);

    private bool Carries() =>
        _lastLineAt is { } last
        && _replies < MostReplies
        && clock() - last <= Window;

    private string Brief()
    {
        var name = commander?.Invoke() is { Length: > 0 } named ? $" The Commander: {named}" : string.Empty;
        var last = _replies >= MostReplies ? " This is your last reply: bring the passage to a close." : string.Empty;

        return $"""
                 {FlavourBriefs.Narration.Speaker}{name}
                 {FlavourBriefs.Narration.Instruction}
                 What the Commander says to you is what the Commander said or did in this moment of the story: narrate
                 it and what followed, in the third person. Never address the Commander as "you". The passage so far:

                 {string.Join('\n', _said)}{last}
                 """;
    }

    [GeneratedRegex(@"\b(you|your|yours|yourself|you're|you've|you'll|you'd)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecondPerson();
}
