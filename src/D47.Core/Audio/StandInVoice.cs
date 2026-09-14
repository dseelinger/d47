using System.Reflection;

namespace D47.Core.Audio;

/// <summary>
/// The bundled Kokoro clip the Guardian voice Test button falls back to when nothing free can prove
/// the treatments on (#226) — rendered by <c>tools/gen-standin.py</c> and embedded the way the cues
/// are.
/// </summary>
public static class StandInVoice
{
    private const string ResourceName = "D47.Core.StandIn.guardian-standin";

    private static readonly Lazy<AudioClip> Loaded = new(Load);

    public static AudioClip Clip => Loaded.Value;

    private static AudioClip Load()
    {
        var assembly = typeof(StandInVoice).Assembly;

        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} could not be opened.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        return WavReader.Read(buffer, "stand-in voice");
    }
}
