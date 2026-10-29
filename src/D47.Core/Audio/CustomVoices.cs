using System.Security.Cryptography;
using System.Text.Json;
using D47.Core.Configuration;
using D47.Core.Storage;

namespace D47.Core.Audio;

/// <summary>A custom Chatterbox voice's index row.</summary>
public sealed record CustomVoice(string Id, string Name, string Gender, string Pitch, string Pace)
{
    public string Locale => CustomVoices.Locale;
}

/// <summary>
/// The Commander's custom Chatterbox voices: 24 kHz mono 16-bit PCM, each kept protected in
/// <c>data\voices\custom\&lt;id&gt;.bin</c> and decrypted into memory only, with an index in
/// <c>voices.json</c>. Nothing here writes plaintext audio to disk.
/// </summary>
public sealed class CustomVoices
{
    public const string IdPrefix = "my-";

    public const string Locale = "en";

    public const int MaxNameLength = 40;

    public const string DefaultPitch = "mid";

    public const string DefaultPace = "even";

    public const int SampleRate = ChatterboxVoices.SampleRate;

    private const int IdHexDigits = 8;

    private static readonly string[] Genders = ["female", "male"];

    private static readonly JsonSerializerOptions IndexOptions = new() { WriteIndented = true };

    private readonly IFileSystem _files;
    private readonly ISecretProtector _protector;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _versions = new(StringComparer.Ordinal);

    public CustomVoices(string dataFolder, IFileSystem files, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(protector);

        Folder = Path.Combine(dataFolder, "voices", "custom");
        _files = files;
        _protector = protector;
    }

    public string Folder { get; }

    public string IndexPath => Path.Combine(Folder, "voices.json");

    /// <summary>Raised after a voice is saved, renamed or deleted.</summary>
    public event Action? Changed;

    /// <summary>True for <c>my-</c> followed by 8 lower-case hex digits.</summary>
    public static bool IsId(string? id) =>
        id is not null
        && id.Length == IdPrefix.Length + IdHexDigits
        && id.StartsWith(IdPrefix, StringComparison.Ordinal)
        && id.AsSpan(IdPrefix.Length).IndexOfAnyExcept("0123456789abcdef") < 0;

    /// <summary>Changes on every save and delete of <paramref name="id"/>, so a cache built from earlier audio can tell it is stale.</summary>
    public int Version(string id)
    {
        lock (_gate)
        {
            return _versions.GetValueOrDefault(id);
        }
    }

    public IReadOnlyList<CustomVoice> List()
    {
        lock (_gate)
        {
            return ReadIndex();
        }
    }

    /// <summary>
    /// Trims the leading silence of <paramref name="samples"/>, resamples to 24 kHz, keeps at most
    /// <see cref="ChatterboxVoices.MaxLength"/> and saves it protected. Returns why it was refused, or null.
    /// </summary>
    public string? Save(string name, string? gender, string? pitch, string? pace, float[] samples, int sampleRate) =>
        Save(name, gender, pitch, pace, samples, sampleRate, out _);

    /// <summary>As the overload without <paramref name="id"/>, giving the new voice's id when it was saved.</summary>
    public string? Save(
        string name, string? gender, string? pitch, string? pace, float[] samples, int sampleRate, out string? id)
    {
        ArgumentNullException.ThrowIfNull(samples);

        id = null;
        name = name?.Trim() ?? string.Empty;
        gender ??= string.Empty;
        pitch = string.IsNullOrEmpty(pitch) ? DefaultPitch : pitch;
        pace = string.IsNullOrEmpty(pace) ? DefaultPace : pace;

        if (gender.Length > 0 && !Genders.Contains(gender))
        {
            return $"The gender \"{gender}\" is not female, male or empty.";
        }

        if (!ChatterboxCatalog.Pitches.Contains(pitch) || !ChatterboxCatalog.Paces.Contains(pace))
        {
            return $"The pitch \"{pitch}\" or the pace \"{pace}\" is not a band.";
        }

        if (NameProblem(name, [], except: null) is { } nameProblem)
        {
            return nameProblem;
        }

        if (Refuse(samples, sampleRate) is { } reason)
        {
            return reason;
        }

        var (start, length) = Kept(samples, sampleRate);
        var pcm = Pcm24(samples.AsSpan(start, length), sampleRate);

        var sealedBytes = _protector.Protect(pcm);
        Array.Clear(pcm);

        lock (_gate)
        {
            var voices = ReadIndex();

            if (NameProblem(name, voices, except: null) is { } taken)
            {
                return taken;
            }

            string fresh;

            do
            {
                fresh = IdPrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(IdHexDigits / 2));
            }
            while (voices.Any(voice => voice.Id == fresh));

            _files.WriteBytes(AudioPath(fresh), sealedBytes);
            WriteIndex([.. voices, new CustomVoice(fresh, name, gender, pitch, pace)]);
            _versions[fresh] = _versions.GetValueOrDefault(fresh) + 1;
            id = fresh;
        }

        Changed?.Invoke();
        return null;
    }

    /// <summary>Why <see cref="Save(string, string?, string?, string?, float[], int)"/> would refuse <paramref name="samples"/> for their sound, or null.</summary>
    public static string? Refuse(float[] samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var start = FirstSound(samples);

        return start >= samples.Length
            ? OwnVoice.Refuse(samples, sampleRate)
            : OwnVoice.Refuse(samples.AsSpan(start), sampleRate);
    }

    /// <summary>The samples a save keeps: from the first sound, at most <see cref="ChatterboxVoices.MaxLength"/>.</summary>
    public static (int Start, int Length) Kept(float[] samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var start = Math.Min(FirstSound(samples), samples.Length);
        var most = (int)(ChatterboxVoices.MaxLength.TotalSeconds * sampleRate);

        return (start, Math.Min(samples.Length - start, most));
    }

    /// <summary>The audio a save of <paramref name="samples"/> would keep, as a clip to play back.</summary>
    public static AudioClip Preview(string name, float[] samples, int sampleRate)
    {
        var (start, length) = Kept(samples, sampleRate);

        return new AudioClip(name, Pcm24(samples.AsSpan(start, length), sampleRate), new AudioFormat(SampleRate, 1));
    }

    /// <summary>Why <paramref name="name"/> cannot name a voice, or null; <paramref name="except"/> is the voice being renamed.</summary>
    public string? NameProblem(string name, string? except = null)
    {
        name = name?.Trim() ?? string.Empty;

        lock (_gate)
        {
            return NameProblem(name, ReadIndex(), except);
        }
    }

    /// <summary>Returns why <paramref name="name"/> cannot be used, or null when the voice was renamed.</summary>
    public string? Rename(string id, string name)
    {
        name = name?.Trim() ?? string.Empty;

        lock (_gate)
        {
            var voices = ReadIndex();

            if (voices.All(voice => voice.Id != id))
            {
                return "That voice no longer exists.";
            }

            if (NameProblem(name, voices, except: id) is { } problem)
            {
                return problem;
            }

            WriteIndex([.. voices.Select(voice => voice.Id == id ? voice with { Name = name } : voice)]);
        }

        Changed?.Invoke();
        return null;
    }

    public void Delete(string id)
    {
        lock (_gate)
        {
            var voices = ReadIndex();

            if (voices.All(voice => voice.Id != id))
            {
                return;
            }

            WriteIndex([.. voices.Where(voice => voice.Id != id)]);
            _files.Delete(AudioPath(id));
            _versions[id] = _versions.GetValueOrDefault(id) + 1;
        }

        Changed?.Invoke();
    }

    /// <summary>The voice at 24 kHz mono, or null when it is not saved or does not decrypt for this Windows user.</summary>
    public float[]? Load(string id)
    {
        if (Pcm(id) is not { } pcm)
        {
            return null;
        }

        var samples = PcmConverter.Pcm16ToFloats(pcm);

        Array.Clear(pcm);
        return samples;
    }

    /// <summary>The voice as a clip to play back, or null when it is not saved.</summary>
    public AudioClip? Clip(string id) =>
        List().FirstOrDefault(voice => voice.Id == id) is { } voice && Pcm(id) is { } pcm
            ? new AudioClip(voice.Name, pcm, new AudioFormat(SampleRate, 1))
            : null;

    /// <summary>16-bit PCM at 24 kHz.</summary>
    private static byte[] Pcm24(ReadOnlySpan<float> samples, int sampleRate)
    {
        var resampled = PcmResample.To(samples, sampleRate, SampleRate);
        var kept = Math.Min(resampled.Length, (int)(ChatterboxVoices.MaxLength.TotalSeconds * SampleRate));
        var pcm = PcmConverter.FloatsToPcm16(resampled.AsSpan(0, kept));
        Array.Clear(resampled);
        return pcm;
    }

    private static int FirstSound(float[] samples)
    {
        var floor = (float)Math.Pow(10, OwnVoice.SilenceDbfs / 20);

        for (var i = 0; i < samples.Length; i++)
        {
            if (Math.Abs(samples[i]) > floor)
            {
                return i;
            }
        }

        return samples.Length;
    }

    private static string? NameProblem(string name, IReadOnlyList<CustomVoice> voices, string? except)
    {
        if (name.Length is < 1 or > MaxNameLength)
        {
            return $"A voice name is 1 to {MaxNameLength} characters.";
        }

        return voices.Any(voice => voice.Id != except && string.Equals(voice.Name, name, StringComparison.OrdinalIgnoreCase))
            ? $"A custom voice is already named \"{name}\"."
            : null;
    }

    private string AudioPath(string id) => Path.Combine(Folder, id + ".bin");

    private List<CustomVoice> ReadIndex()
    {
        if (_files.ReadBytes(IndexPath) is not { } stored)
        {
            return [];
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<CustomVoice>>(stored) ?? [];

            return [.. rows.Where(row => IsId(row.Id) && !string.IsNullOrWhiteSpace(row.Name))];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void WriteIndex(List<CustomVoice> voices)
    {
        _files.WriteBytes(IndexPath, JsonSerializer.SerializeToUtf8Bytes(voices, IndexOptions));
    }

    private byte[]? Pcm(string id)
    {
        if (!IsId(id))
        {
            return null;
        }

        byte[]? sealedBytes;

        lock (_gate)
        {
            sealedBytes = _files.ReadBytes(AudioPath(id));
        }

        if (sealedBytes is null)
        {
            return null;
        }

        return _protector.TryUnprotect(sealedBytes, out var pcm) ? pcm : null;
    }
}
