using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace D47.Tts;

/// <summary>Kokoro, run on this machine (#101, Phase 59).</summary>
public sealed partial class KokoroTtsProvider : ITtsProvider, IDisposable
{
    public const string ProviderId = "kokoro";

    private readonly IFileSystem _files;
    private readonly string _folder;
    private readonly string? _pronunciations;
    private readonly ILogger<KokoroTtsProvider> _logger;
    private readonly Lock _gate = new();
    private readonly RunsInFlight _runs;

    private readonly string? _dictionaryFolder;
    private readonly Action<string>? _japaneseMissing;
    private readonly JapanesePhonemiser _japanesePhonemiser;

    private InferenceSession? _session;
    private Dictionary<string, long>? _vocabulary;
    private Phonemiser? _phonemiser;
    private JapaneseReader? _reader;

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
    /// <param name="japaneseDictionary">Where the IPAdic files are, or null where no Japanese voice may speak.</param>
    /// <param name="japaneseMissing">Called with a Japanese voice asked to speak before its file or the dictionary is here.</param>
    public KokoroTtsProvider(
        IFileSystem files,
        string folder,
        ILogger<KokoroTtsProvider> logger,
        string? pronunciations = null,
        string? japaneseDictionary = null,
        Action<string>? japaneseMissing = null)
    {
        _files = files;
        _folder = folder;
        _logger = logger;
        _pronunciations = pronunciations;
        _dictionaryFolder = japaneseDictionary;
        _japaneseMissing = japaneseMissing;
        _japanesePhonemiser = new JapanesePhonemiser(word => _logger.LogDebug("\"{Word}\" has no reading and is left out", word));
        _runs = new RunsInFlight(FreeSession);
    }

    public string Id => ProviderId;

    public string Name => "Kokoro (on this machine)";

    /// <summary>The English voices, which are files on disk rather than an answer from a service.</summary>
    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Catalogue(KokoroAssets.IsInstalled(_files, _folder), null));

    /// <summary>The voices that speak <paramref name="language"/>, null for English: the 28 English voices, or the five Japanese ones.</summary>
    public static VoiceCatalogue Catalogue(bool installed, string? language)
    {
        if (!installed)
        {
            return VoiceCatalogue.Unreachable(
                $"The local voice is not downloaded yet. It is about {KokoroAssets.TotalMegabytes:0} MB, "
                + "fetched once, and then nothing D47 speaks leaves this machine.");
        }

        var ids = SpokenLanguage.IsEnglish(language) ? KokoroAssets.VoiceIds
            : string.Equals(SpokenLanguage.Primary(language!), SpokenLanguage.Japanese, StringComparison.OrdinalIgnoreCase)
                ? KokoroAssets.JapaneseVoiceIds
                : [];

        var voices = ids
            .Select(id => new VoiceInfo(
                id,
                KokoroAssets.Name(id),
                id[0] == 'j' ? "ja-JP" : SpokenLetters.AccentOf(id) == SpeechAccent.British ? "en-GB" : "en-US",
                id[1] == 'f' ? "Female" : "Male"))
            .ToList();

        return VoiceCatalogue.Of(voices);
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

        if (!KokoroAssets.IsInstalled(_files, _folder))
        {
            return null;
        }

        Phonemiser phonemiser;

        lock (_gate)
        {
            phonemiser = LoadPhonemiser();
        }

        return VoiceIdFor(voice) switch
        {
            { } japanese when IsJapanese(japanese) => JapaneseIsHere(japanese) ? JapanesePhonemes(text) : null,
            { } voiceId => phonemiser.ToPhonemes(text, voiceId),
            null => null,
        };
    }

    /// <summary>
    /// The chosen voice where Kokoro has it, a chosen Japanese voice where this provider reads Japanese, the first
    /// English voice when none is chosen and Kokoro is installed, or null.
    /// </summary>
    private string? VoiceIdFor(VoiceSelection voice)
    {
        if (_dictionaryFolder is not null && IsJapanese(voice.VoiceId))
        {
            return voice.VoiceId;
        }

        return InterimVoice.For(voice.VoiceId, KokoroAssets.IsInstalled(_files, _folder) ? KokoroAssets.VoiceIds : [])
            is { } chosen && KokoroAssets.VoiceIds.Contains(chosen)
            ? chosen
            : null;
    }

    private static bool IsJapanese(string? voiceId) => KokoroAssets.JapaneseVoice(voiceId) is not null;

    /// <summary>Whether the Japanese voice's file and the dictionary are both on this machine.</summary>
    private bool JapaneseIsHere(string voiceId) =>
        _dictionaryFolder is { } dictionary
        && KokoroAssets.JapaneseVoice(voiceId) is { } asset
        && _files.Stat(Path.Combine(_folder, "voices", voiceId + ".bin")) is { } file && file.Length == asset.Bytes
        && JapaneseDictionaryAssets.IsInstalled(_files, dictionary);

    /// <summary>Throws where a Japanese voice cannot speak yet, reporting the voice as missing first.</summary>
    private void EnsureJapanese(string voiceId)
    {
        if (!KokoroAssets.IsInstalled(_files, _folder))
        {
            throw new TtsException("The local voice is not downloaded, so Kokoro has no Japanese voice to speak in.");
        }

        if (JapaneseIsHere(voiceId))
        {
            return;
        }

        _japaneseMissing?.Invoke(voiceId);

        throw new TtsException(
            $"Kokoro's Japanese voice {KokoroAssets.Name(voiceId)} or the dictionary it reads Japanese with was not on this PC "
            + "when a line was due, so the story's own voice spoke it. Both are downloaded once.");
    }

    /// <summary>The phonemes for a line of Japanese, read with the dictionary.</summary>
    private string JapanesePhonemes(string text)
    {
        JapaneseReader reader;

        lock (_gate)
        {
            reader = _reader ??= new JapaneseReader(_files, _dictionaryFolder!);
        }

        return _japanesePhonemiser.ToPhonemes(reader.Read(JapanesePhonemiser.Normalise(text)));
    }

    private AudioClip Speak(string text, VoiceSelection voice, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (VoiceIdFor(voice) is not { } voiceId)
        {
            throw new TtsException(voice.VoiceId is { Length: > 0 } unknown
                ? $"Kokoro has no voice called {unknown}. Pick one in Settings."
                : "No Kokoro voice has been chosen. Pick one in Settings.",
                fault: TtsFault.NoVoice,
                settingKey: SpeechCapability.LocalVoiceKey);
        }

        var japanese = IsJapanese(voiceId);

        if (japanese)
        {
            EnsureJapanese(voiceId);
        }

        var (session, vocabulary, phonemiser) = Load();

        try
        {
            var phonemes = japanese ? JapanesePhonemes(text) : phonemiser.ToPhonemes(text, voiceId);
            var samples = Pieces(phonemes)
                .Select(piece => Encode(piece, vocabulary))
                .Where(tokens => tokens.Length > 2)
                .SelectMany(tokens => Run(session, tokens, Style(voiceId, tokens.Length), (float)voice.Rate))
                .ToArray();

            // Nothing sayable came out.
            if (samples.Length == 0)
            {
                return new AudioClip(text, ReadOnlyMemory<byte>.Empty, AudioFormat.Standard);
            }

            // 24 kHz to the arbiter's 48, the exact doubling both the other local-ish paths use.
            var pcm = PcmUpsample.Double(PcmConverter.FloatsToPcm16(samples));

            return new AudioClip(text, pcm, AudioFormat.Standard);
        }
        finally
        {
            lock (_gate)
            {
                _runs.End();
            }
        }
    }

    /// <summary>
    /// The session, the vocabulary and the dictionary, built once and kept. Counts a run that the caller ends
    /// through <c>_runs</c>; throws <see cref="ObjectDisposedException"/> once disposed.
    /// </summary>
    private (InferenceSession Session, Dictionary<string, long> Vocabulary, Phonemiser Phonemiser) Load()
    {
        lock (_gate)
        {
            if (!_runs.TryBegin())
            {
                throw new ObjectDisposedException(nameof(KokoroTtsProvider));
            }

            try
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
            catch
            {
                _runs.End();
                throw;
            }
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
                _files,
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
            PhonemeDictionary.Read(_files, Path.Combine(_folder, "phoneme_dict.json"), _logger),
            Overrides(_vocabulary),
            Note);
    }

    /// <summary>
    /// The phoneme vocabulary, read from the file rather than transcribed into the source: a
    /// hand-copied vocabulary is a silent mismatch waiting to happen.
    /// </summary>
    private Dictionary<string, long> ReadVocabulary()
    {
        var path = Path.Combine(_folder, "tokenizer.json");
        using var document = JsonDocument.Parse(
            _files.ReadText(path) ?? throw new FileNotFoundException("No Kokoro tokenizer.", path));

        var vocabulary = document.RootElement.GetProperty("model").GetProperty("vocab");
        var map = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var entry in vocabulary.EnumerateObject())
        {
            map[entry.Name] = entry.Value.GetInt64();
        }

        return map;
    }

    /// <summary>The most phonemes the model takes in one run.</summary>
    private const int MostPhonemes = 510;

    /// <summary>
    /// The phonemes in runs the model can take: whole where they fit, else broken after punctuation, then at a space,
    /// then anywhere. No run is longer than <see cref="MostPhonemes"/> characters, so none encodes to more tokens.
    /// </summary>
    private static IReadOnlyList<string> Pieces(string phonemes)
    {
        if (phonemes.Length <= MostPhonemes)
        {
            return [phonemes];
        }

        var pieces = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var part in PieceBreaks().Split(phonemes))
        {
            if (current.Length + part.Length > MostPhonemes && current.Length > 0)
            {
                pieces.Add(current.ToString());
                current.Clear();
            }

            var rest = part;

            while (rest.Length > MostPhonemes)
            {
                var space = rest.LastIndexOf(' ', MostPhonemes - 1);
                var cut = space > 0 ? space + 1 : MostPhonemes;
                pieces.Add(rest[..cut]);
                rest = rest[cut..];
            }

            current.Append(rest);
        }

        if (current.Length > 0)
        {
            pieces.Add(current.ToString());
        }

        return pieces;
    }

    [System.Text.RegularExpressions.GeneratedRegex("(?<=[.!?,;:—] )", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex PieceBreaks();

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
        var path = Path.Combine(_folder, "voices", voiceId + ".bin");
        var bytes = _files.ReadBytes(path) ?? throw new FileNotFoundException("No Kokoro voice file.", path);
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

    /// <summary>Frees the session now if no line is running, otherwise when the last line ends.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _runs.Dispose();
        }
    }

    private void FreeSession()
    {
        _session?.Dispose();
        _session = null;
        _reader?.Dispose();
        _reader = null;
    }
}
