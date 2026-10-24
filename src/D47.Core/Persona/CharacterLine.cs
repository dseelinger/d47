using D47.Core.Audio;
using D47.Core.Characters;
using D47.Core.Conversation;

namespace D47.Core.Persona;

/// <summary>The Commander's custom characters, each on the line while addressed by name.</summary>
/// <param name="characters">The active Commander's characters.</param>
/// <param name="commanderSystem">The system the Commander is in.</param>
/// <param name="distance">Light years between two systems, or null when the host cannot ask.</param>
/// <param name="modelFree">Whether an unnamed utterance has a route that needs no model; the ship takes it and the line stays open.</param>
/// <param name="others">Names that belong to someone else aboard; one at the front of an utterance closes the line.</param>
public sealed class CharacterLine(
    Func<IReadOnlyList<CustomCharacter>> characters,
    Func<string?> commanderSystem,
    Func<string, string, CancellationToken, Task<double?>>? distance = null,
    Func<string, bool>? modelFree = null,
    Func<IReadOnlyList<string>>? others = null) : ILine
{
    private readonly Dictionary<string, List<ConversationMessage>> _transcripts = [];

    private string? _open;

    public bool IsOpen => _open is not null;

    public void Close() => _open = null;

    public async Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken)
    {
        var all = characters();

        if (all.Count == 0)
        {
            return NotMine();
        }

        CustomCharacter character;
        string question;

        if (Named(input, all) is var (named, rest))
        {
            character = named;
            question = rest;
        }
        else if (others?.Invoke().Any(name => CrewAddressing.Opens(input, name) is not null) == true)
        {
            return NotMine();
        }
        else if (all.FirstOrDefault(c => c.Id == _open) is { } current)
        {
            if (modelFree?.Invoke(input) == true)
            {
                return new LineDecision.NotMine();
            }

            character = current;
            question = input.Trim();
        }
        else
        {
            return NotMine();
        }

        double? lightYears = null;

        if (!character.Aboard && character.System is { Length: > 0 } system)
        {
            lightYears = await CarrierComms.DistanceAsync(commanderSystem(), system, distance, cancellationToken)
                .ConfigureAwait(false);

            if (lightYears is > CarrierComms.RangeLightYears and var away)
            {
                _open = null;

                return new LineDecision.Refused(OutOfRange(character.Name, away), TurnRoute.CarrierOutOfRange);
            }
        }

        _open = CaptainLine.IsDismissal(input, character.Name) ? null : character.Id;

        if (!_transcripts.TryGetValue(character.Id, out var transcript))
        {
            _transcripts[character.Id] = transcript = [];
        }

        return new LineDecision.Taken(
            new Speaker(
                character.Aboard ? VoiceRole.Crew : VoiceRole.Comms,
                character.Name,
                Brief(character),
                transcript,
                OffersTools: false,
                Signal: character.Aboard ? 1 : LinkSignal.Strength(lightYears),
                Voice: character.Voice,
                Picture: character.Picture),
            question.Length == 0 ? "The Commander is trying to get your attention." : question);
    }

    /// <summary>The refusal for a character who cannot hear the Commander from <paramref name="away"/>.</summary>
    public static string OutOfRange(string name, double away) =>
        $"{name} is {away:N0} light years away, beyond comms range.";

    /// <summary>The prompt block for a character: the name and the Commander's notes.</summary>
    public static string Brief(CustomCharacter character) =>
        $"""
         You are {character.Name}, a person the Commander knows. You are a human being, not an artificial
         intelligence, and you never claim to be one.

         You answer briefly, the way somebody does on {(character.Aboard ? "an intercom" : "a radio")}. You do not narrate and you do not
         speak for the core, which is a separate voice and can answer for itself.

         You know only what a person in your place would know. You have no access to galactic databases,
         no tools, and no way to look anything up. Asked something outside what you could know, you say so plainly.{CarrierComms.NotesBlock(character.Notes)}
         """;

    private static (CustomCharacter Character, string Question)? Named(string input, IReadOnlyList<CustomCharacter> all)
    {
        foreach (var character in all.OrderByDescending(c => c.Name.Length))
        {
            if (CrewAddressing.Opens(input, character.Name) is { } question)
            {
                return (character, question);
            }
        }

        return null;
    }

    private LineDecision NotMine()
    {
        _open = null;
        return new LineDecision.NotMine();
    }
}
