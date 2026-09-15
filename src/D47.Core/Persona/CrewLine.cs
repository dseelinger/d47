using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>A hired pilot, on the intercom while addressed.</summary>
/// <param name="crew">The roster, or null while there is no active Commander.</param>
/// <param name="shipName">What the pilot calls the hull they are posted to, for their own brief.</param>
/// <param name="shipAiName">What the Commander calls the ship's AI; at the front of an utterance it closes the line.</param>
public sealed class CrewLine(Func<ShipCrew?> crew, Func<string?> shipName, Func<string?> shipAiName) : ILine
{
    private readonly Dictionary<long, List<ConversationMessage>> _transcripts = [];

    private CrewMember? _open;

    public bool IsOpen => _open is not null;

    public void Close() => _open = null;

    public Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken)
    {
        var roster = crew();

        if (roster is not { Any: true })
        {
            _open = null;
            return Task.FromResult<LineDecision>(new LineDecision.NotMine());
        }

        if (shipAiName() is { Length: > 0 } shipAi && CrewAddressing.Opens(input, shipAi) is not null)
        {
            _open = null;
            return Task.FromResult<LineDecision>(new LineDecision.NotMine());
        }

        // The captain's own line claims this, not the fighter bay's.
        if (CrewAddressing.Opens(input, NpcChatter.CaptainName) is not null)
        {
            _open = null;
            return Task.FromResult<LineDecision>(new LineDecision.NotMine());
        }

        string question;
        CrewMember member;

        if (CrewAddressing.Match(input, roster) is { } addressed)
        {
            member = addressed.Member;
            question = addressed.Question;
        }
        else if (_open is { } current && roster.Members.Any(m => m.CrewId == current.CrewId))
        {
            member = current;
            question = input.Trim();
        }
        else
        {
            _open = null;
            return Task.FromResult<LineDecision>(new LineDecision.NotMine());
        }

        _open = CaptainLine.IsDismissal(input, member.Name) ? null : member;

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
