using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Seats;

namespace D47.Core.Persona;

/// <summary>A crew seat on the ship flown, on the intercom while addressed.</summary>
/// <param name="seats">The seats on the ship flown, empty when there are none.</param>
/// <param name="shipName">What the ship is called, for the seat's brief.</param>
/// <param name="hull">The hull as it is said, for the seat's brief.</param>
/// <param name="crew">The hired pilots; a pilot's name at the front of an utterance closes the line.</param>
/// <param name="shipAiName">What the Commander calls the ship's AI; at the front of an utterance it closes the line.</param>
public sealed class SeatLine(
    Func<IReadOnlyList<CrewSeat>> seats,
    Func<string?> shipName,
    Func<string?> hull,
    Func<ShipCrew?> crew,
    Func<string?> shipAiName) : ILine
{
    private readonly Dictionary<string, List<ConversationMessage>> _transcripts = [];

    private string? _open;

    public bool IsOpen => _open is not null;

    public void Close() => _open = null;

    public Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken)
    {
        var aboard = seats();

        if (aboard.Count == 0
            || (shipAiName() is { Length: > 0 } shipAi && CrewAddressing.Opens(input, shipAi) is not null)
            || CrewAddressing.Opens(input, NpcChatter.CaptainName) is not null)
        {
            return NotMine();
        }

        string question;
        CrewSeat seat;

        if (CrewAddressing.MatchSeat(input, aboard) is var (named, rest))
        {
            seat = named;
            question = rest;
        }
        else if (crew() is { Any: true } roster && CrewAddressing.Match(input, roster) is not null)
        {
            return NotMine();
        }
        else if (aboard.FirstOrDefault(s => s.Id == _open) is { } current)
        {
            seat = current;
            question = input.Trim();
        }
        else
        {
            return NotMine();
        }

        _open = CaptainLine.IsDismissal(input, seat.Name) ? null : seat.Id;

        if (!_transcripts.TryGetValue(seat.Id, out var transcript))
        {
            _transcripts[seat.Id] = transcript = [];
        }

        return Task.FromResult<LineDecision>(new LineDecision.Taken(
            new Speaker(
                VoiceRole.Crew,
                seat.Name,
                CrewAddressing.SeatBrief(seat, shipName(), hull()),
                transcript,
                OffersTools: false,
                Signal: 1),
            question.Length == 0 ? "The Commander is trying to get your attention." : question));
    }

    private Task<LineDecision> NotMine()
    {
        _open = null;
        return Task.FromResult<LineDecision>(new LineDecision.NotMine());
    }
}
