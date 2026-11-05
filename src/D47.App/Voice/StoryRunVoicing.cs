using System.Globalization;
using D47.Core.Audio;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>Why a story run's line is shown for reading rather than spoken.</summary>
public enum StoryLineHold
{
    None,

    /// <summary>No voice provider can speak it.</summary>
    NoProvider,

    /// <summary>The provider was asked and failed.</summary>
    Failed,

    /// <summary>The speaker's voice is one the Commander asked d47 to forget.</summary>
    Forgotten,
}

/// <summary>Who speaks a recorded line now: the role, the voice for it, and a cast member's voice where it is one.</summary>
public sealed record StoryLineSpeech(VoiceRole Role, VoiceSelection Voice, PinnedVoice? Pinned = null);

/// <summary>A line's clip to play in the standard format, or why it is held for reading, with the provider's error.</summary>
public sealed record SpokenAgain(AudioClip? Clip, StoryLineHold Hold = StoryLineHold.None, string? Detail = null)
{
    public static SpokenAgain Held(StoryLineHold hold, string? detail = null) => new(null, hold, detail);
}

/// <summary>
/// Speaks again a story run's line that kept no clip, in the speaker's current voice, and keeps the new clip in the run's
/// record so it is synthesised once. Never synthesises in a voice the Commander asked to forget.
/// </summary>
/// <param name="record">The record the clip is kept in, or null when there is none.</param>
/// <param name="speechOf">Who speaks the line now, or null when nobody can.</param>
/// <param name="forgotten">Whether a voice id is the Commander's own or custom voice and has been deleted.</param>
/// <param name="render">Renders the line's text in the speech given, reporting progress, without playing it.</param>
public sealed class StoryRunVoicing(
    Func<StoryRunRecord?> record,
    Func<string?> commander,
    Func<StoryRunLine, StoryLineSpeech?> speechOf,
    Func<string, bool> forgotten,
    Func<StoryLineSpeech, string, Action<double>, Task<RenderedLine>> render,
    ILogger logger)
{
    private readonly Dictionary<string, Task<SpokenAgain>> _speaking = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Raised when a line's new clip was kept in its run's record; on any thread.</summary>
    public event Action<Story>? Kept;

    /// <summary>The clip for <paramref name="line"/>: the one kept in the record, or a new one, kept there; requests made while it is made share it.</summary>
    /// <param name="progress">Raised with the share synthesised so far while a new clip is made.</param>
    public Task<SpokenAgain> SpeakAsync(Story run, StoryRunLine line, Action<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(line);

        var key = KeyOf(run, line);
        Task<SpokenAgain> task;

        lock (_gate)
        {
            if (_speaking.TryGetValue(key, out var asked))
            {
                return asked;
            }

            task = Task.Run(async () =>
            {
                // Only a synthesis under way is shared; a finished one is read back from the record, so a voice
                // forgotten since is not played from memory.
                try
                {
                    return await SpeakNowAsync(run, line, progress ?? (_ => { })).ConfigureAwait(false);
                }
                finally
                {
                    lock (_gate)
                    {
                        _speaking.Remove(key);
                    }
                }
            });
            _speaking[key] = task;
        }

        return task;
    }

    /// <summary>What the Commander is told when a line is held for reading.</summary>
    public static string Why(StoryLineHold hold) => hold switch
    {
        StoryLineHold.Forgotten => "You asked d47 to forget this speaker's voice. The line is shown instead; it is not spoken.",
        StoryLineHold.NoProvider => "No voice provider can speak this line. The line is shown instead; it is not spoken.",
        _ => "The voice provider did not answer. The line is shown instead; it is not spoken.",
    };

    private static string KeyOf(Story run, StoryRunLine line) =>
        $"{run.Id}\n{run.PickedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}\n{line.Key}";

    private async Task<SpokenAgain> SpeakNowAsync(Story run, StoryRunLine line, Action<double> progress)
    {
        var frontierId = commander();
        var kept = record();

        if (kept?.Read(frontierId, run).FirstOrDefault(each => string.Equals(each.Key, line.Key, StringComparison.Ordinal)) is { Clip: not null } now
            && kept.Clip(frontierId, run, now) is { } clip)
        {
            return new SpokenAgain(PcmConverter.ToStandard(clip));
        }

        if (speechOf(line) is not { } speech)
        {
            return SpokenAgain.Held(StoryLineHold.NoProvider);
        }

        if (new[] { speech.Voice.VoiceId, speech.Pinned?.VoiceId, speech.Pinned?.Fallback?.VoiceId }
            .Any(id => id is { Length: > 0 } && forgotten(id)))
        {
            return SpokenAgain.Held(StoryLineHold.Forgotten);
        }

        RenderedLine rendered;

        try
        {
            rendered = await render(speech, line.Body, progress).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not speak again story line {Key}", line.Key);
            return SpokenAgain.Held(StoryLineHold.Failed, ex.Message);
        }

        if (rendered.Clip is not { } spoken)
        {
            return rendered.Failure is null ? SpokenAgain.Held(StoryLineHold.NoProvider) : SpokenAgain.Held(StoryLineHold.Failed, rendered.Failure);
        }

        if (kept?.KeepClip(frontierId, run, line, spoken) is not null)
        {
            Kept?.Invoke(run);
        }

        return new SpokenAgain(PcmConverter.ToStandard(spoken.Joined(line.Key)));
    }
}
