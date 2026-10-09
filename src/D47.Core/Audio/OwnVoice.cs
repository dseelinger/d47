using D47.Core.Configuration;

namespace D47.Core.Audio;

/// <summary>
/// The Commander's own recorded voice: 24 kHz mono 16-bit PCM, kept protected in
/// <c>data\voice\own.bin</c> and decrypted into memory only. Nothing here writes plaintext to disk.
/// </summary>
public sealed class OwnVoice
{
    /// <summary>The voice id a story's cast names; Chatterbox lists it as Your voice while a recording is saved.</summary>
    public const string VoiceId = "own";

    public const string Sentence = "This is my voice, and d47 may use it for my stories on this PC.";

    public const int SampleRate = ChatterboxVoices.SampleRate;

    /// <summary>A recording whose loudest sample is at or below this is refused as silence.</summary>
    public const double SilenceDbfs = -40;

    public static readonly TimeSpan MinLength = ChatterboxVoices.MinLength;

    public static readonly TimeSpan MaxLength = ChatterboxVoices.MaxLength;

    private readonly ISecretProtector _protector;
    private readonly Lock _gate = new();
    private int _version;

    public OwnVoice(string dataFolder, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(protector);

        FilePath = Path.Combine(dataFolder, "voice", "own.bin");
        _protector = protector;
    }

    public string FilePath { get; }

    /// <summary>Raised after a recording is saved or deleted.</summary>
    public event Action? Changed;

    /// <summary>Changes on every save and delete, so a cache built from an earlier recording can tell it is stale.</summary>
    public int Version => Volatile.Read(ref _version);

    public bool Exists => File.Exists(FilePath);

    /// <summary>Why <paramref name="samples"/> cannot be kept, or null when it can.</summary>
    public static string? Refuse(ReadOnlySpan<float> samples, int sampleRate)
    {
        var seconds = (double)samples.Length / sampleRate;

        if (seconds < MinLength.TotalSeconds)
        {
            return $"The recording is {seconds:0.0} s long and needs at least {MinLength.TotalSeconds:0} s. "
                + "Read the whole sentence at an easy pace.";
        }

        var peak = 0f;

        foreach (var sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        return peak <= Math.Pow(10, SilenceDbfs / 20)
            ? $"Nothing in the recording was louder than {SilenceDbfs:0} dBFS, so the microphone heard silence. "
                + "Check the input device is not muted and record again."
            : null;
    }

    /// <summary>
    /// Resamples <paramref name="samples"/> to 24 kHz, keeps at most <see cref="MaxLength"/>, and saves it
    /// protected. Returns why it was refused, or null when it was saved.
    /// </summary>
    public string? Save(float[] samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (Refuse(samples, sampleRate) is { } reason)
        {
            return reason;
        }

        var resampled = PcmResample.To(samples, sampleRate, SampleRate);
        var kept = Math.Min(resampled.Length, (int)(MaxLength.TotalSeconds * SampleRate));
        var pcm = new byte[kept * 2];

        for (var i = 0; i < kept; i++)
        {
            var value = (short)(Math.Clamp(resampled[i], -1f, 1f) * short.MaxValue);

            pcm[i * 2] = (byte)value;
            pcm[(i * 2) + 1] = (byte)(value >> 8);
        }

        var sealedBytes = _protector.Protect(pcm);
        Array.Clear(pcm);
        Array.Clear(resampled);

        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            var staging = FilePath + ".tmp";
            File.WriteAllBytes(staging, sealedBytes);
            File.Move(staging, FilePath, overwrite: true);
            Interlocked.Increment(ref _version);
        }

        Changed?.Invoke();
        return null;
    }

    /// <summary>The recording at 24 kHz mono, or null when none is saved or it does not decrypt for this Windows user.</summary>
    public float[]? Load()
    {
        if (Pcm() is not { } pcm)
        {
            return null;
        }

        var samples = new float[pcm.Length / 2];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(pcm[i * 2] | (pcm[(i * 2) + 1] << 8)) / 32768f;
        }

        Array.Clear(pcm);
        return samples;
    }

    /// <summary>The recording as a clip to play back, or null when none is saved.</summary>
    public AudioClip? Clip() =>
        Pcm() is { } pcm ? new AudioClip(Sentence, pcm, new AudioFormat(SampleRate, 1)) : null;

    public void Delete()
    {
        lock (_gate)
        {
            if (!File.Exists(FilePath))
            {
                return;
            }

            File.Delete(FilePath);
            Interlocked.Increment(ref _version);
        }

        Changed?.Invoke();
    }

    private byte[]? Pcm()
    {
        byte[] sealedBytes;

        lock (_gate)
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            sealedBytes = File.ReadAllBytes(FilePath);
        }

        return _protector.TryUnprotect(sealedBytes, out var pcm) ? pcm : null;
    }
}
