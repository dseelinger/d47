using D47.Core.Audio;
using D47.Core.Characters;
using D47.Core.Conversation;

namespace D47.Core.Persona;

/// <summary>The Commander's custom characters, each on the CHARACTERS channel by id.</summary>
/// <param name="characters">The active Commander's characters.</param>
/// <param name="commanderSystem">The system the Commander is in.</param>
/// <param name="distance">Light years between two systems, or null when the host cannot ask.</param>
public sealed class CharacterLine(
    Func<IReadOnlyList<CustomCharacter>> characters,
    Func<string?> commanderSystem,
    Func<string, string, CancellationToken, Task<double?>>? distance = null) : ILine
{
    private readonly Dictionary<string, List<ConversationMessage>> _transcripts = [];

    public async Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selected);

        if (selected is not { Channel: CommsChannel.Characters, SubChannel.Key: var id }
            || characters().FirstOrDefault(c => c.Id == id) is not { } character)
        {
            return new LineDecision.NotMine();
        }

        var question = input.Trim();
        double? lightYears = null;

        if (!character.Aboard && character.System is { Length: > 0 } system)
        {
            lightYears = await CarrierComms.DistanceAsync(commanderSystem(), system, distance, cancellationToken)
                .ConfigureAwait(false);

            if (lightYears is > CarrierComms.RangeLightYears and var away)
            {
                return new LineDecision.Refused(OutOfRange(character.Name, away), TurnRoute.CarrierOutOfRange);
            }
        }

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
}
