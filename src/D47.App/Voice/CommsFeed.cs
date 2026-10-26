using D47.App.Panel;
using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.App.Voice;

/// <summary>Builds what the comms channels read from the host's state.</summary>
public static class CommsFeed
{
    /// <summary>
    /// The inputs for the active Commander. Reads only what is already in memory, so it may run on the tick.
    /// </summary>
    public static CommsInputs Inputs(
        CommanderGameState? active,
        IReadOnlyList<NpcHeard> heard,
        RunningCast? story,
        IReadOnlyList<CustomCharacter> characters,
        bool narratorAnswers = false)
    {
        ArgumentNullException.ThrowIfNull(heard);
        ArgumentNullException.ThrowIfNull(characters);

        return new CommsInputs
        {
            CarrierOwned = active?.Carrier.Owned == true,
            Target = active?.Target,
            Heard = heard,
            StoryRunning = story is not null,
            NarratorAnswers = narratorAnswers,
            StoryCast = story is null
                ? []
                : [.. story.Members.Where(member => !member.Recorded).Select(member => new AdventureSpeaker(member.Shown.Id, member.Shown.Name, string.Empty) { Role = member.Role })],
            Crew = active?.Crew ?? ShipCrew.Empty,
            Characters = characters,
        };
    }
}
