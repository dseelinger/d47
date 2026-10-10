using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Messages;
using D47.Core.Persona;
using D47.Core.Stories;

namespace D47.App.Voice;

/// <summary>Keeps what the Commander heard of a story: the message for a spoken line, and the story's own feed.</summary>
internal sealed class StoryRecords(
    MessageStore messages,
    StoryDirector stories,
    D47.Core.Adventures.AdventureBook adventures,
    PersonaHost personas,
    GameStateStore gameState,
    Func<StorySpeakerShown?, string?> pictureOf)
{
    /// <summary>Keeps a spoken clip on the message posted for it. Called on the pool once synthesis has finished.</summary>
    internal void Keep(D47Message? posted, SpokenClip? spoken)
    {
        if (posted is not null && spoken is not null)
        {
            messages.Attach(posted.Key, spoken);
        }
    }

    /// <summary>A spoken clue, marked given and posted to Messages from whoever said it.</summary>
    internal void RecordClue(Announcement announcement, SpokenClip? spoken)
    {
        if (StoryClueCallout.Parse(announcement.Key) is not { } due)
        {
            return;
        }

        var commander = gameState.Active?.Identity.FrontierId;
        var title = stories.Clue(commander, due)?.Title ?? due.StoryId;
        var voice = stories.ClueVoice(commander, due);

        stories.ClueGiven(commander, due);

        messages.Post(
            voice?.From ?? (announcement.Voice == VoiceRole.Narrator ? MessageStore.Narrator : personas.Current.Id),
            title,
            announcement.Text,
            DateTimeOffset.Now,
            announcement.Key,
            picture: pictureOf(voice?.Cast),
            spoken: spoken,
            cast: voice?.Cast?.Picture);
    }

    /// <summary>A beat, as it was said, onto the story's own feed (asked for 2026-08-22).</summary>
    internal void RecordAdventure(Announcement announcement, SpokenClip? spoken)
    {
        if (D47.Core.Adventures.AdventureCallout.Spoken(announcement.Key) is not var (key, beat, line))
        {
            return;
        }

        var commander = gameState.Active?.Identity.FrontierId;
        var story = adventures.Store.Find(commander, key);
        var reached = beat >= 0 ? story?.Beats.ElementAtOrDefault(beat) : null;
        var voice = story?.StoryId is null ? null : stories.LineVoice(commander, story.SpeakerOf(beat, line));

        D47.Core.Adventures.AdventureMessages.Post(
            messages, voice?.From ?? personas.Current.Id, story, key, beat, announcement.Text, DateTimeOffset.Now, spoken, pictureOf(voice?.Cast), voice?.Cast?.Picture);

        adventures.Told(commander, key, new D47.Core.Adventures.AdventureTold
        {
            Kind = D47.Core.Adventures.AdventureToldKind.Beat,
            Text = announcement.Text,
            At = DateTimeOffset.Now,
            Beat = beat,
            Line = line,
            Speaker = voice?.Cast?.Name ?? (voice is { } said ? VoiceRoles.Called(said.Role) : null),
            Title = reached?.Title ?? (beat < 0 ? "Opening" : null),

            // Stored rather than derived later: a story edited after a beat has fired would otherwise
            // re-describe what the Commander did with the trigger it has now.
            Trigger = reached?.Trigger.Describe(),
        });
    }

    /// <summary>A spoken nudge, posted to Messages from the narrator and kept on the story's feed.</summary>
    internal void RecordNudge(Announcement announcement, SpokenClip? spoken)
    {
        if (NarratorCallout.Nudged(announcement.Key) is not { } key)
        {
            return;
        }

        var commander = gameState.Active?.Identity.FrontierId;
        var story = adventures.Standing(commander, key);
        var now = DateTimeOffset.Now;

        messages.Post("narrator", story?.Adventure.Name ?? key, announcement.Text, now, key, spoken: spoken);

        adventures.Told(commander, key, new D47.Core.Adventures.AdventureTold
        {
            Kind = D47.Core.Adventures.AdventureToldKind.Nudge,
            Text = announcement.Text,
            At = now,
            Title = story?.CurrentBeat?.Title,
            Trigger = story?.CurrentBeat?.Trigger.Describe(),
        });
    }
}
