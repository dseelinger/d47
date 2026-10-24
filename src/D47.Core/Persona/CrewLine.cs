using System.Globalization;
using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>A hired pilot, on the CREW channel by crew id.</summary>
/// <param name="crew">The roster, or null while there is no active Commander.</param>
/// <param name="shipName">What the pilot calls the hull they are posted to, for their own brief.</param>
public sealed class CrewLine(
    Func<ShipCrew?> crew,
    Func<string?> shipName) : ILine
{
    private readonly Dictionary<long, List<ConversationMessage>> _transcripts = [];

    public Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selected);

        if (selected is not { Channel: CommsChannel.Crew, SubChannel.Key: var key }
            || crew()?.Members.FirstOrDefault(m => m.Active && m.CrewId.ToString(CultureInfo.InvariantCulture) == key) is not { } member)
        {
            return Task.FromResult<LineDecision>(new LineDecision.NotMine());
        }

        var question = input.Trim();

        if (!_transcripts.TryGetValue(member.CrewId, out var transcript))
        {
            _transcripts[member.CrewId] = transcript = [];
        }

        return Task.FromResult<LineDecision>(new LineDecision.Taken(
            new Speaker(
                VoiceRole.Crew,
                member.Name,
                CrewAddressing.Brief(member, shipName()),
                transcript,
                OffersTools: false,
                // An intercom does not fade.
                Signal: 1),
            question.Length == 0 ? "The Commander is trying to get your attention." : question));
    }
}
