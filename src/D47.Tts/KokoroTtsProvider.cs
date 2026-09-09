using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace D47.Tts;

/// <summary>Kokoro, run on this machine (#101, Phase 59).</summary>
public sealed class KokoroTtsProvider : ITtsProvider, IDisposable
{
    public const string ProviderId = "kokoro";

    /// <summary>What Kokoro emits, before the arbiter's doubling.</summary>
    private const int ModelSampleRate = 24_000;

    private readonly string _folder;
    private readonly string? _pronunciations;
    private readonly ILogger<KokoroTtsProvider> _logger;
    private readonly Lock _gate = new();

    private InferenceSession? _session;
    private Dictionary<string, long>? _vocabulary;
    private Phonemiser? _phonemiser;

    /// <summary>
    /// <param name="folder">Where the model, the voices and the dictionary are.</param> <param
    /// name="logger"> Also where the ladder says its work: at Debug it names the rung every segment
    /// came off, which is the line #153 was investigated without.
    /// </summary>
    /// <param name="folder">Where the model, the voices and the dictionary are.</param>
    /// <param name="logger">
    /// Also where the ladder says its work: at Debug it names the rung every segment came off, which is
    /// the line #153 was investigated without.
    /// </param>
    /// <param name="pronunciations">
    /// The Commander's own corrections (#150), or null where nothing may override the ladder — which is
    /// what a provider built for an audition rather than for the app wants.
    /// </param>
    public KokoroTtsProvider(
        string folder,
        ILogger<KokoroTtsProvider> logger,
        string? pronunciations = null)
    {
        _folder = folder;
        _logger = logger;
        _pronunciations = pronunciations;
    }

    public string Id => ProviderId;

    public string Name => "Kokoro (on this machine)";

    /// <summary>The voices, which are files on disk rather than an answer from a service.</summary>
    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        if (!KokoroAssets.IsInstalled(_folder))
        {
            return Task.FromResult(VoiceCatalogue.Unreachable(
                $"The local voice is not downloaded yet. It is about {KokoroAssets.TotalMegabytes:0} MB, "
                + "fetched once, and then nothing D47 speaks leaves this machine."));
        }

        var voices = KokoroAssets.VoiceIds
            .Select(id => new VoiceInfo(
                id,
                KokoroAssets.Name(id),
                SpokenLetters.AccentOf(id) == SpeechAccent.British ? "en-GB" : "en-US",
                id[1] == 'f' ? "Female" : "Male"))
            .ToList();

        return Task.FromResult(VoiceCatalogue.Of(voices));
    }

    /// <summary>One line, spoken here.</summary>
    public Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Speak(text, voice, cancellationToken), cancellationToken);

    /// <summary>The phonemes this line will actually be spoken as (#164).</summary>
    public string? Phonemes(string text, VoiceSelection voice)
    {
        ArgumentNullException.ThrowIfNull(voice);

        if (!KokoroAssets.IsInstalled(_folder))
        {
            return null;
        }

        Phonemiser phonemiser;

        lock (_gate)
        {
            phonemiser = LoadPhonemiser();
        }

        return phonemiser.ToPhonemes(text, VoiceIdFor(voice));
    }

    /// <summary>
    /// The voice actually spoken in: the chosen one where Kokoro has it, and its own default where it
    /// does not.
    /// </summary>
    private static string VoiceIdFor(VoiceSelection voice) =>
        voice.VoiceId is { Length: > 0 } chosen && KokoroAssets.VoiceIds.Contains(chosen)
            ? chosen
            : "af_heart";

    private AudioClip Speak(string text, VoiceSelection voice, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var voiceId = VoiceIdFor(voice);

        var (session, vocabulary, phonemiser) = Load();

        var phonemes = phonemiser.ToPhonemes(text, voiceId);
        var tokens = Encode(phonemes, vocabulary);

        // Nothing sayable came out.
        if (tokens.Length <= 2)
        {
            return new AudioClip(text, ReadOnlyMemory<byte>.Empty, AudioFormat.Standard);
        }

        var style = Style(voiceId, tokens.Length);
        var samples = Run(session, tokens, style, (float)voice.Rate);

        // 24 kHz to the arbiter's 48, the exact doubling both the other local-ish paths use.
        var pcm = PcmUpsample.Double(ToPcm(samples));

        return new AudioClip(text, pcm, AudioFormat.Standard);
    }

    /// <summary>The session, the vocabulary and the dictionary, built once and kept.</summary>
    private (InferenceSession Session, Dictionary<string, long> Vocabulary, Phonemiser Phonemiser) Load()
    {
        lock (_gate)
        {
            if (_session is not null && _vocabulary is not null && _phonemiser is not null)
            {
                return (_session, _vocabulary, _phonemiser);
            }

            var started = System.Diagnostics.Stopwatch.StartNew();

            _session ??= new InferenceSession(Path.Combine(_folder, "model.onnx"));
            _vocabulary ??= ReadVocabulary();
            var phonemiser = LoadPhonemiser();

            _logger.LogInformation(
                "The local voice is loaded ({Milliseconds} ms)", started.ElapsedMilliseconds);

            return (_session, _vocabulary, phonemiser);
        }
    }

    /// <summary>
    /// The Commander's correction file, watching this voice's own symbol set, or null where this
    /// provider was built without one.
    /// </summary>
    private PronunciationOverrides? Overrides(Dictionary<string, long> vocabulary) =>
        _pronunciations is null
            ? null
            : new PronunciationOverrides(
                _pronunciations,
                vocabulary.Keys
                    .Where(symbol => symbol.Length == 1)
                    .Select(symbol => symbol[0])
                    .ToHashSet(),

                // A rejected entry is named once per version of the file, at Warning: it is a thing to go and
                // fix, and a Commander who edited a file and heard no change needs to be told why without
                // turning anything up.
                entry => _logger.LogWarning("{Entry}", entry));

    /// <summary>Which rung of the ladder a segment came off.</summary>
    private void Note(string segment, PhonemeRung rung, string ipa) =>
        _logger.LogDebug(
            "\"{Segment}\" came off the {Rung} rung as {Ipa}", segment, rung, ipa);

    /// <summary>
    /// The phonemiser, built once and kept, with the Commander's corrections aboard (#150) so a phoneme
    /// trace says what would actually be spoken.
    /// </summary>
    private Phonemiser LoadPhonemiser()
    {
        _vocabulary ??= ReadVocabulary();

        return _phonemiser ??= new Phonemiser(
            PhonemeDictionary.Read(Path.Combine(_folder, "phoneme_dict.json"), _logger),
            Overrides(_vocabulary),
            Note);
    }

    /// <summary>
    /// The phoneme vocabulary, read from the file rather than transcribed into the source: a
    /// hand-copied vocabulary is a silent mismatch waiting to happen.
    /// </summary>
    private Dictionary<string, long> ReadVocabulary()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(_folder, "tokenizer.json")));

        var vocabulary = document.RootElement.GetProperty("model").GetProperty("vocab");
        var map = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var entry in vocabulary.EnumerateObject())
        {
            map[entry.Name] = entry.Value.GetInt64();
        }

        return map;
    }

    /// <summary>IPA to token ids.</summary>
    private static long[] Encode(string phonemes, Dictionary<string, long> vocabulary)
    {
        var ids = new List<long> { 0 };
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(phonemes);

        while (elements.MoveNext())
        {
            var symbol = (string)elements.Current;

            if (vocabulary.TryGetValue(symbol, out var id))
            {
                ids.Add(id);
                continue;
            }

            foreach (var rune in symbol.EnumerateRunes())
            {
                if (vocabulary.TryGetValue(rune.ToString(), out var part))
                {
                    ids.Add(part);
                }
            }
        }

        ids.Add(0);
        return [.. ids];
    }

    /// <summary>One voice's style vector for a line of this length.</summary>
    private float[] Style(string voiceId, int tokens)
    {
        var bytes = File.ReadAllBytes(Path.Combine(_folder, "voices", voiceId + ".bin"));
        var floats = new float[bytes.Length / 4];

        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);

        const int Dimensions = 256;

        var buckets = floats.Length / Dimensions;
        var bucket = Math.Clamp(tokens, 0, buckets - 1);

        return floats[(bucket * Dimensions)..((bucket + 1) * Dimensions)];
    }

    private static float[] Run(InferenceSession session, long[] ids, float[] style, float rate)
    {
        var tokens = new DenseTensor<long>(ids, [1, ids.Length]);
        var styleTensor = new DenseTensor<float>(style, [1, style.Length]);

        // Spelled out rather than a collection expression: [rate] binds to the dimensions overload and fails
        // as "cannot convert float to int", which reads like a different bug entirely.
        var speed = new DenseTensor<float>(new Memory<float>([Math.Clamp(rate, 0.5f, 2.0f)]), [1]);

        using var results = session.Run(
        [
            NamedOnnxValue.CreateFromTensor("input_ids", tokens),
            NamedOnnxValue.CreateFromTensor("style", styleTensor),
            NamedOnnxValue.CreateFromTensor("speed", speed),
        ]);

        return [.. results.First().AsEnumerable<float>()];
    }

    /// <summary>Float samples to the 16-bit PCM everything downstream deals in.</summary>
    private static byte[] ToPcm(float[] samples)
    {
        var pcm = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);

            pcm[i * 2] = (byte)value;
            pcm[(i * 2) + 1] = (byte)(value >> 8);
        }

        return pcm;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
