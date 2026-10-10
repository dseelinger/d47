namespace D47.Core.Audio;

/// <summary>The voice a line is spoken in when none is chosen: the provider's first listed voice. Used to speak only, never saved.</summary>
public static class InterimVoice
{
    /// <summary>The chosen voice, else the first of <paramref name="listed"/>, else null.</summary>
    public static string? For(string? chosen, IEnumerable<string> listed)
    {
        ArgumentNullException.ThrowIfNull(listed);

        return string.IsNullOrWhiteSpace(chosen) ? listed.FirstOrDefault() : chosen;
    }

    /// <summary>
    /// The selection as given when it names a voice; otherwise with the provider's first listed voice, fetching the
    /// list if the provider does not hold it. Still names no voice when the list is empty or could not be fetched.
    /// </summary>
    public static async Task<VoiceSelection> OrFirstListedAsync(
        this ITtsProvider provider,
        VoiceSelection voice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(voice);

        if (!string.IsNullOrWhiteSpace(voice.VoiceId))
        {
            return voice;
        }

        var listed = await provider.ListVoicesAsync(cancellationToken).ConfigureAwait(false);

        return For(null, listed.Voices.Select(known => known.Id)) is { } first ? voice with { VoiceId = first } : voice;
    }
}
