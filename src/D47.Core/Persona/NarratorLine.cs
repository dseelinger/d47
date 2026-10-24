using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// The Narrator, on the STORY channel, who answers what the Commander says with more narration.
/// Thread-safe: <see cref="Heard"/> runs on the callout drain and <see cref="RouteAsync"/> on the turn loop.
/// </summary>
/// <param name="hiddenStory">The running story's hidden layer as the Narrator reads it, or null.</param>
/// <param name="commander">How the Narrator names the Commander when there is no character sheet, or null.</param>
public sealed partial class NarratorLine(
    Func<string?> hiddenStory,
    Func<string?>? commander = null,
    Func<string?>? model = null,
    Func<ShipFacts>? facts = null,
    ILogger? logger = null) : ILine
{
    public const string Name = "Narrator";

    private const string KeyPrefix = "narrator.reply";

    private readonly Lock _lock = new();

    /// <summary>The last narration and its replies since, as "Narrator: text" and "Commander: text".</summary>
    private readonly List<string> _said = [];

    /// <summary>Called with each reply the Narrator says.</summary>
    public Action<string>? Said { get; set; }

    /// <summary>Records a narration as spoken, which starts the passage afresh.</summary>
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
        if (selected is not { Channel: CommsChannel.Story, SubChannel.Key: CommsChannels.NarratorKey })
        {
            return new LineDecision.NotMine();
        }

        var said = input.Trim();

        if (said.Length > 0)
        {
            _said.Add($"Commander: {said}");
        }

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
            said.Length == 0 ? "The Commander waits." : said);
    }

    /// <summary>The reply, or nothing when it may not be said or speaks to the Commander.</summary>
    private string Screened(string reply)
    {
        var said = reply.Trim();

        var sayable = FlavourBriefs.MayBeSpoken(said)
                      && !AddressesTheCommander(said)
                      && ContradictedClaims.Sayable(said, facts?.Invoke() ?? ShipFacts.Unknown, logger, KeyPrefix) is not null;

        if (!sayable)
        {
            return string.Empty;
        }

        lock (_lock)
        {
            _said.Add($"{Name}: {said}");
        }

        Said?.Invoke(said);

        return said;
    }

    /// <summary>Whether a narrated reply speaks to the Commander in the second person.</summary>
    public static bool AddressesTheCommander(string reply) => SecondPerson().IsMatch(reply);

    private string Brief()
    {
        var name = commander?.Invoke() is { Length: > 0 } named ? $" The Commander: {named}" : string.Empty;

        return $"""
                 {FlavourBriefs.Narration.Speaker}{name}
                 {FlavourBriefs.Narration.Instruction}
                 What the Commander says to you is what the Commander said or did in this moment of the story: narrate
                 it and what followed, in the third person. Never address the Commander as "you". The passage so far:

                 {ExchangeLines.Recent(_said)}
                 """;
    }

    [GeneratedRegex(@"\b(you|your|yours|yourself|you're|you've|you'll|you'd)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecondPerson();
}
