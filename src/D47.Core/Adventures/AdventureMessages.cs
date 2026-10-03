using D47.Core.Audio;
using D47.Core.Messages;

namespace D47.Core.Adventures;

/// <summary>A spoken beat, filed as a written message.</summary>
public static class AdventureMessages
{
    public static D47Message Post(
        MessageStore messages, string from, Adventure? story, string key, int beat, string text, DateTimeOffset now, SpokenClip? spoken = null, string? picture = null, string? cast = null)
    {
        var reached = beat >= 0 ? story?.Beats.ElementAtOrDefault(beat) : null;

        return messages.Post(from, reached?.Title ?? story?.Name ?? key, text, now, key, picture: picture, spoken: spoken, cast: cast);
    }
}
