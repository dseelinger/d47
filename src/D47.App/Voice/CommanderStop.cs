using D47.Core.Audio;

namespace D47.App.Voice;

/// <summary>Cancel and "stop": cuts what D47 is doing, and pauses a podcast or book as the Commander's pause only when D47 did not interrupt it.</summary>
public static class CommanderStop
{
    public static void Silence(AudioArbiter audio, StoryRunPlayer? story, BookPlayer? book)
    {
        ArgumentNullException.ThrowIfNull(audio);

        audio.Silence(() =>
        {
            story?.Stop();
            book?.PauseUnlessInterrupted();
        });
    }
}
