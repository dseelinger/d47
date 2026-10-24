using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// A running story's cast member, on the STORY channel by cast id, who answers in the first person.
/// Thread-safe: <see cref="Heard"/> runs on the callout drain and <see cref="RouteAsync"/> on the turn loop.
/// </summary>
/// <param name="member">
/// The running story's cast member with this id, as the Commander meets them; null when there is none, or the story
/// is paused, switched off or without Odyssey.
/// </param>
/// <param name="hiddenStory">The running story's hidden layer as every speaker reads it, or null.</param>
public sealed class CastLine(
    Func<string, AdventureSpeaker?> member,
    Func<string?> hiddenStory,
    Func<ShipFacts>? facts = null,
    ILogger? logger = null) : ILine
{
    private const string KeyPrefix = "cast.reply";

    private readonly Lock _lock = new();

    /// <summary>Each member's last line and the replies since, as "Name: text" and "Commander: text".</summary>
    private readonly Dictionary<string, List<string>> _said = new(StringComparer.Ordinal);

    /// <summary>Records a line the cast member <paramref name="castId"/> spoke, which starts their exchange afresh.</summary>
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
                _said.Remove(castId);
                return;
            }

            _said[castId] = [$"{speaking.Name}: {line.Trim()}"];
        }
    }

    public Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selected);

        lock (_lock)
        {
            return Task.FromResult(Route(selected, input));
        }
    }

    private LineDecision Route(CommsSelection selected, string input)
    {
        if (selected is not { Channel: CommsChannel.Story, SubChannel.Key: var castId } || member(castId) is not { } speaking)
        {
            return new LineDecision.NotMine();
        }

        var said = input.Trim();

        if (!_said.TryGetValue(castId, out var exchange))
        {
            _said[castId] = exchange = [];
        }

        if (said.Length > 0)
        {
            exchange.Add($"Commander: {said}");
        }

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.Crew,
                speaking.Name,
                Brief(speaking, exchange),
                [],
                OffersTools: false,
                Signal: 1,
                Screen: reply => Screened(reply, castId, speaking.Name),
                HiddenStory: hiddenStory,
                Cast: speaking.Id),
            said.Length == 0 ? "The Commander is trying to get your attention." : said);
    }

    /// <summary>The reply, or nothing when it may not be said.</summary>
    private string Screened(string reply, string castId, string name)
    {
        var said = reply.Trim();

        var sayable = FlavourBriefs.MayBeSpoken(said)
                      && ContradictedClaims.Sayable(said, facts?.Invoke() ?? ShipFacts.Unknown, logger, KeyPrefix) is not null;

        if (!sayable)
        {
            return string.Empty;
        }

        lock (_lock)
        {
            if (_said.TryGetValue(castId, out var exchange))
            {
                exchange.Add($"{name}: {said}");
            }
        }

        return said;
    }

    /// <summary>The Story log body of one reply: what the Commander said, then the answer.</summary>
    public static string Logged(string asked, string answer) => $"You: {asked.Trim()}\n\n{answer.Trim()}";

    private static string Brief(AdventureSpeaker speaking, List<string> exchange)
    {
        var notes = speaking.Notes is { Length: > 0 } written ? $" The Commander's notes on {speaking.Name}: {written}" : string.Empty;

        return $"""
                 You are {speaking.Name}, a person in the story the Commander is playing. {speaking.Who}{notes} {StorySpeaker.MannerRule}
                 The Commander is talking to you. Reply as {speaking.Name}, in the first person, speaking to the Commander,
                 in one to three short sentences, as said aloud. You are not the core and never speak for it. You
                 have no tools and change nothing aboard. The exchange so far:

                 {ExchangeLines.Recent(exchange)}
                 """;
    }
}
