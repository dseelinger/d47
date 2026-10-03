namespace D47.Core.Audio;

/// <summary>What one line sounded like as it was queued to play, and the provider and voice that spoke it.</summary>
public sealed record SpokenClip(IReadOnlyList<AudioClip> Parts, string Provider, string? VoiceId)
{
    /// <summary>Whether it was spoken in the Commander's own recorded voice.</summary>
    public bool Own => string.Equals(VoiceId, OwnVoice.VoiceId, StringComparison.OrdinalIgnoreCase);

    /// <summary>The parts as one clip, converted to <see cref="AudioFormat.Standard"/> where their formats differ.</summary>
    public AudioClip Joined(string name)
    {
        if (Parts.Count == 1)
        {
            return Parts[0] with { Name = name };
        }

        var parts = Parts.All(part => part.Format == Parts[0].Format)
            ? Parts
            : [.. Parts.Select(PcmConverter.ToStandard)];
        var pcm = new byte[parts.Sum(part => part.Pcm.Length)];
        var at = 0;

        foreach (var part in parts)
        {
            part.Pcm.Span.CopyTo(pcm.AsSpan(at));
            at += part.Pcm.Length;
        }

        return new AudioClip(name, pcm, parts[0].Format);
    }
}
