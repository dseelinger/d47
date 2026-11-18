using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>
/// Chatterbox Turbo, run on this machine's CPU from a reference clip: one that ships beside the exe, or one fetched
/// from the voice release the first time it is picked, previewed or spoken. A line whose language is Japanese is
/// spoken by Chatterbox Multilingual from the same clip.
/// </summary>
public sealed class ChatterboxTtsProvider : ITtsProvider, IDisposable
{
    public const string ProviderId = TtsProviderCatalog.ChatterboxId;

    private readonly string _modelFolder;
    private readonly string _voicesFolder;
    private readonly string _fetchedFolder;
    private readonly Func<Uri, long, CancellationToken, Task<byte[]>> _download;
    private readonly IFileSystem _files;
    private readonly ILogger<ChatterboxTtsProvider> _logger;
    private readonly Func<IChatterboxEngine> _open;
    private readonly Func<bool> _installed;
    private readonly Lock _gate = new();
    private readonly Lock _load = new();
    private readonly OwnVoice? _own;
    private readonly CustomVoices? _custom;
    private readonly ChatterboxJapaneseSetup? _japanese;

    /// <summary>Turbo and its encoded voices, and Multilingual and its own; each guarded by <c>_gate</c>.</summary>
    private readonly Loaded _turbo = new("Chatterbox");
    private readonly Loaded _multilingual = new("Chatterbox Multilingual");

    /// <summary>Guards the sets below.</summary>
    private readonly Lock _clips = new();
    private readonly HashSet<string> _verified = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<bool>> _fetching = new(StringComparer.Ordinal);

    /// <summary>Voices whose fetch failed this session, with the reason; a line does not ask again, a pick or a preview does.</summary>
    private readonly Dictionary<string, string> _failed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failureLogged = new(StringComparer.Ordinal);
    private readonly HashSet<string> _standInLogged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustomVoice> _customKnown = new(StringComparer.Ordinal);

    private readonly PronunciationOverrides? _overrides;
    private readonly HashSet<string> _ipaLogged = new(StringComparer.OrdinalIgnoreCase);

    private ChatterboxTokeniser? _tokeniser;
    private ChatterboxMultilingualTokeniser? _japaneseTokeniser;
    private IReadOnlyList<ChatterboxVoice>? _voices;
    private bool _disposed;

    /// <param name="modelFolder">Where <see cref="ChatterboxInstaller"/> put the graphs and tokenizer.</param>
    /// <param name="voicesFolder">Where <c>voices.tsv</c>, <c>catalog.tsv</c> and the shipped clips are.</param>
    /// <param name="fetchedFolder">Where fetched clips are kept.</param>
    /// <param name="own">The Commander's own recording, listed as <see cref="OwnVoice.VoiceId"/> while one is saved.</param>
    /// <param name="custom">The Commander's custom voices, listed after the catalogue.</param>
    /// <param name="pronunciations">The Commander's <c>pronunciations.json</c>, whose respellings are applied before a line is encoded.</param>
    /// <param name="multilingualFolder">Where Chatterbox Multilingual's graphs and tokenizer are; null where no line is spoken in Japanese.</param>
    /// <param name="japaneseDictionary">Where the IPAdic files a Japanese line is read with are.</param>
    public ChatterboxTtsProvider(
        IFileSystem files,
        string modelFolder,
        string voicesFolder,
        string fetchedFolder,
        ILogger<ChatterboxTtsProvider> logger,
        OwnVoice? own = null,
        CustomVoices? custom = null,
        string? pronunciations = null,
        string? multilingualFolder = null,
        string? japaneseDictionary = null)
        : this(
            files,
            modelFolder,
            voicesFolder,
            fetchedFolder,
            logger,
            () => ChatterboxPipeline.Open(modelFolder, PerformanceCores.ForThisMachine()),
            () => ChatterboxAssets.IsInstalled(files, modelFolder),
            ChatterboxClipDownload.GetAsync,
            own,
            custom,
            pronunciations,
            multilingualFolder is null || japaneseDictionary is null
                ? null
                : ChatterboxJapaneseSetup.OnThisMachine(files, multilingualFolder, japaneseDictionary))
    {
    }

    internal ChatterboxTtsProvider(
        IFileSystem files,
        string modelFolder,
        string voicesFolder,
        string fetchedFolder,
        ILogger<ChatterboxTtsProvider> logger,
        Func<IChatterboxEngine> open,
        Func<bool> installed,
        Func<Uri, long, CancellationToken, Task<byte[]>> download,
        OwnVoice? own = null,
        CustomVoices? custom = null,
        string? pronunciations = null,
        ChatterboxJapaneseSetup? japanese = null)
    {
        _files = files;
        _japanese = japanese;
        _overrides = pronunciations is null
            ? null
            : new PronunciationOverrides(
                files,
                pronunciations,
                speakable: null,
                complain: entry => logger.LogWarning("{Entry}", entry));
        _modelFolder = modelFolder;
        _voicesFolder = voicesFolder;
        _fetchedFolder = fetchedFolder;
        _download = download;
        _logger = logger;
        _open = open;
        _installed = installed;
        _own = own;
        _custom = custom;

        if (own is not null)
        {
            own.Changed += OnOwnVoiceChanged;
        }

        if (custom is not null)
        {
            custom.Changed += OnCustomVoicesChanged;
        }
    }

    public string Id => ProviderId;

    public string Name => "Chatterbox (on this machine)";

    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default)
    {
        if (!_installed())
        {
            return Task.FromResult(VoiceCatalogue.Unreachable(
                $"Chatterbox is not downloaded yet. It is about {ChatterboxAssets.TotalMegabytes:0} MB, "
                + "fetched once, and then nothing D47 speaks through it leaves this machine."));
        }

        return Task.FromResult(VoiceCatalogue.Of([.. Voices().Select(voice => voice.Voice), .. CustomListed()]));
    }

    /// <summary>Your voice while a recording is saved, then every custom voice.</summary>
    private List<VoiceInfo> CustomListed()
    {
        var listed = new List<VoiceInfo>();

        if (_own is { Exists: true })
        {
            listed.Add(new VoiceInfo(OwnVoice.VoiceId, "Your voice", CustomVoices.Locale) { Custom = true });
        }

        if (_custom is not null)
        {
            foreach (var voice in _custom.List())
            {
                lock (_clips)
                {
                    _customKnown[voice.Id] = voice;
                }

                listed.Add(Info(voice));
            }
        }

        return listed;
    }

    private static VoiceInfo Info(CustomVoice voice) =>
        new(voice.Id, voice.Name, voice.Locale, voice.Gender.Length > 0 ? voice.Gender : null)
        {
            Custom = true,
            Description = $"{voice.Pitch} pitch, {voice.Pace} pace",
            Pitch = voice.Pitch,
            Pace = voice.Pace,
        };

    /// <summary>Every voice with its pitch and pace bands, whether or not its clip is on this machine.</summary>
    public IReadOnlyList<ChatterboxVoice> Catalogue() => Voices();

    /// <summary>How long a line waits for a clip being fetched before it is spoken in a stand-in.</summary>
    internal TimeSpan FirstLineWait { get; init; } = ChatterboxCatalog.FirstLineWait;

    public Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            async () =>
            {
                var japanese = IsJapanese(voice);

                if (japanese)
                {
                    EnsureJapanese();
                }

                return Speak(text, await SpokenAsync(voice, cancellationToken).ConfigureAwait(false), japanese, cancellationToken);
            },
            cancellationToken);

    /// <summary>The sentence a Japanese line throws while Chatterbox Multilingual or the dictionary is not on this PC.</summary>
    public static string JapaneseNotDownloaded =>
        "Chatterbox Japanese is not downloaded, so this line was spoken in the story's own voice. Download Chatterbox "
        + $"Japanese, about {ChatterboxMultilingualAssets.TotalMegabytesText} MB, under Settings, Providers, Speech synthesis.";

    private static bool IsJapanese(VoiceSelection voice) =>
        voice?.Language is { } language
        && string.Equals(SpokenLanguage.Primary(language), SpokenLanguage.Japanese, StringComparison.OrdinalIgnoreCase);

    private void EnsureJapanese()
    {
        if (_japanese?.Installed() != true)
        {
            throw new TtsException(JapaneseNotDownloaded, fault: TtsFault.NotDownloaded, settingKey: SpeechCapability.ChatterboxJapaneseKey);
        }
    }

    /// <summary>
    /// Fetches a voice's clip when it is not on this machine, for a pick or a preview, trying again after a failure.
    /// True once the clip is here; false for an unlisted voice or a failed fetch, whose reason is logged.
    /// </summary>
    public Task<bool> FetchAsync(string voiceId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(voiceId, OwnVoice.VoiceId, StringComparison.Ordinal))
        {
            return Task.FromResult(_own is { Exists: true });
        }

        if (CustomVoices.IsId(voiceId))
        {
            return Task.FromResult(_custom?.List().Any(voice => voice.Id == voiceId) == true);
        }

        if (Find(voiceId) is not { } voice)
        {
            return Task.FromResult(false);
        }

        if (voice.Shipped)
        {
            return Task.FromResult(true);
        }

        lock (_clips)
        {
            _failed.Remove(voice.Voice.Id);
        }

        return Fetching(voice).WaitAsync(cancellationToken);
    }

    /// <summary>
    /// A voice's clip state changed: a fetch started or ended. Raised with the voice id on the thread that started or
    /// ran the fetch, which may be the UI thread.
    /// </summary>
    public event Action<string>? ClipStateChanged;

    /// <summary>Where a voice's clip is, or null for a voice not listed. A fetched clip is read and hashed once a session.</summary>
    public ChatterboxClipState? ClipState(string voiceId)
    {
        if (string.Equals(voiceId, OwnVoice.VoiceId, StringComparison.Ordinal))
        {
            return _own is { Exists: true } ? new ChatterboxClipState(ChatterboxClip.Custom, 0) : null;
        }

        if (CustomVoices.IsId(voiceId))
        {
            return _custom?.List().Any(voice => voice.Id == voiceId) == true
                ? new ChatterboxClipState(ChatterboxClip.Custom, 0)
                : null;
        }

        if (Find(voiceId) is not { } voice)
        {
            return null;
        }

        if (voice.Shipped)
        {
            return new ChatterboxClipState(ChatterboxClip.Shipped, voice.Bytes);
        }

        string? reason;

        lock (_clips)
        {
            if (_fetching.ContainsKey(voiceId))
            {
                return new ChatterboxClipState(ChatterboxClip.Fetching, voice.Bytes);
            }

            _failed.TryGetValue(voiceId, out reason);
        }

        if (IsHere(voice))
        {
            return new ChatterboxClipState(ChatterboxClip.Fetched, voice.Bytes);
        }

        return reason is null
            ? new ChatterboxClipState(ChatterboxClip.NotFetched, voice.Bytes)
            : new ChatterboxClipState(ChatterboxClip.Failed, voice.Bytes) { Reason = reason };
    }

    /// <summary>The paralinguistic tags in <c>tokenizer.json</c>'s <c>added_tokens</c>; none before the download.</summary>
    public bool Performs(string tag)
    {
        if (!_installed())
        {
            return false;
        }

        return Tokeniser().Performs(tag);
    }

    /// <summary>
    /// The voice a line is spoken in: the one asked for once its clip is here, waiting <see cref="FirstLineWait"/> for
    /// a fetch; otherwise a shipped stand-in while the clip is fetched. Null for the Commander's own voice.
    /// </summary>
    private async Task<ChatterboxVoice?> SpokenAsync(VoiceSelection voice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(voice);

        if (!_installed())
        {
            throw new TtsException(
                "Chatterbox is not downloaded yet. Download it in Settings.",
                fault: TtsFault.NotDownloaded,
                settingKey: SpeechCapability.ChatterboxVoiceKey);
        }

        voice = await this.OrFirstListedAsync(voice, cancellationToken).ConfigureAwait(false);

        if (string.Equals(voice.VoiceId, OwnVoice.VoiceId, StringComparison.Ordinal))
        {
            return null;
        }

        if (CustomVoices.IsId(voice.VoiceId))
        {
            return CustomOrStandIn(voice.VoiceId!, voice.Role);
        }

        var chosen = Find(voice.VoiceId)
            ?? throw new TtsException(voice.VoiceId is { Length: > 0 } unknown
                ? $"Chatterbox has no voice called {unknown}. Pick one in Settings."
                : "No Chatterbox voice has been chosen. Pick one in Settings.",
                fault: TtsFault.NoVoice,
                settingKey: SpeechCapability.ChatterboxVoiceKey);

        if (IsHere(chosen))
        {
            return chosen;
        }

        bool failed;

        lock (_clips)
        {
            failed = _failed.ContainsKey(chosen.Voice.Id);
        }

        if (!failed)
        {
            var fetch = Fetching(chosen);

            if (await Task.WhenAny(fetch, Task.Delay(FirstLineWait, cancellationToken)).ConfigureAwait(false) == fetch
                && await fetch.ConfigureAwait(false))
            {
                return chosen;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        var standIn = ChatterboxCatalog.StandIn(chosen, voice.Role, Voices())
            ?? throw new TtsException(ChatterboxCatalog.NoStandIn(chosen.Voice.Name));

        bool first;

        lock (_clips)
        {
            first = _standInLogged.Add(chosen.Voice.Id);
        }

        if (first)
        {
            _logger.LogInformation(
                "Chatterbox voice {Wanted} is not on this PC yet; its lines are spoken in {StandIn} until it is fetched",
                chosen.Voice.Id,
                standIn.Voice.Id);
        }

        return standIn;
    }

    /// <summary>The custom voice when it is saved and opens for this Windows user; otherwise a shipped stand-in.</summary>
    private ChatterboxVoice CustomOrStandIn(string id, VoiceRole? role)
    {
        CustomVoice? row;

        lock (_clips)
        {
            _customKnown.TryGetValue(id, out row);
        }

        if (_custom?.List().FirstOrDefault(voice => voice.Id == id) is { } saved)
        {
            row = saved;

            lock (_clips)
            {
                _customKnown[id] = saved;
            }

            if (HasCustomEncoded(id) || DecryptsHere(id))
            {
                return new ChatterboxVoice(Info(saved), null, "custom", string.Empty)
                {
                    Shipped = false,
                    Pitch = saved.Pitch,
                    Pace = saved.Pace,
                };
            }
        }

        var wanted = new ChatterboxVoice(
            new VoiceInfo(id, id, CustomVoices.Locale, row?.Gender is { Length: > 0 } gender ? gender : null) { Custom = true },
            null,
            "custom",
            string.Empty)
        {
            Shipped = false,
            Pitch = row?.Pitch,
            Pace = row?.Pace,
        };

        var standIn = ChatterboxCatalog.StandIn(wanted, role, Voices())
            ?? throw new TtsException(
                $"The custom voice {id} is not saved or does not open on this PC, and no shipped voice can stand in.");

        bool first;

        lock (_clips)
        {
            first = _standInLogged.Add(id);
        }

        if (first)
        {
            _logger.LogInformation(
                "Custom voice {Wanted} is not saved or does not open on this PC; its lines are spoken in {StandIn}",
                id,
                standIn.Voice.Id);
        }

        return standIn;
    }

    private bool HasCustomEncoded(string id)
    {
        lock (_gate)
        {
            var version = _custom!.Version(id);

            return _turbo.HasCustom(id, version) || _multilingual.HasCustom(id, version);
        }
    }

    private bool DecryptsHere(string id)
    {
        if (_custom?.Load(id) is not { } samples)
        {
            return false;
        }

        Array.Clear(samples);
        return true;
    }

    /// <summary>A line in Turbo, or in Multilingual where it is Japanese; each engine encodes and keeps its own copy of a voice.</summary>
    private AudioClip Speak(string text, ChatterboxVoice? chosen, bool japanese, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var loaded = japanese ? _multilingual : _turbo;

        if (chosen is null)
        {
            return SpeakOwn(text, japanese, loaded, cancellationToken);
        }

        var ids = Ids(text, japanese);

        if (chosen.Voice.Custom)
        {
            return SpeakCustom(text, ids, chosen.Voice.Id, loaded, cancellationToken);
        }

        // One line at a time: each already takes every performance core.
        lock (_gate)
        {
            ThrowIfDisposed();

            var engine = Engine(loaded);

            if (!loaded.Encoded.TryGetValue(chosen.Voice.Id, out var encoded))
            {
                encoded = engine.Encode(Reference(_files, chosen.ClipPath));
                loaded.Encoded[chosen.Voice.Id] = encoded;
            }

            return Clip(text, engine.Speak(ids, encoded, cancellationToken));
        }
    }

    /// <summary>
    /// Turbo's ids for the line with the Commander's respellings applied; for a Japanese line, Multilingual's ids for the
    /// line read as kana, with no respellings and no tags.
    /// </summary>
    private long[] Ids(string text, bool japanese)
    {
        if (!japanese)
        {
            return Tokeniser().Encode(Respelled(text));
        }

        var prepared = ChatterboxJapaneseText.Prepare(
            text,
            _japanese!.Read,
            word => _logger.LogDebug("\"{Word}\" has no reading and is left out", word));

        return JapaneseTokeniser().Encode(prepared);
    }

    /// <summary>The line with the Commander's respellings applied; an IPA entry is skipped and named once.</summary>
    internal string Respelled(string text)
    {
        if (_overrides is null)
        {
            return text;
        }

        _overrides.Refresh();

        return Respelling.Apply(
            text,
            _overrides,
            skipped =>
            {
                bool first;

                lock (_ipaLogged)
                {
                    first = _ipaLogged.Add(skipped.Key);
                }

                if (first)
                {
                    _logger.LogWarning(
                        "pronunciations.json: \"{Key}\" is IPA, which Chatterbox cannot say; it is spoken as written",
                        skipped.Key);
                }
            });
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, nameof(ChatterboxTtsProvider));

    /// <summary>A line in the Commander's recorded voice, encoded from the recording decrypted into memory.</summary>
    private AudioClip SpeakOwn(string text, bool japanese, Loaded loaded, CancellationToken cancellationToken)
    {
        const string none = "No recording of your voice is saved. Record one in Settings, under Custom voices.";

        if (_own is null || !_own.Exists)
        {
            throw new TtsException(none, settingKey: SpeechCapability.CustomVoicesKey);
        }

        var ids = Ids(text, japanese);

        lock (_gate)
        {
            ThrowIfDisposed();

            var engine = Engine(loaded);
            var version = _own.Version;

            if (loaded.Own is null || loaded.OwnVersion != version)
            {
                loaded.DropOwn();

                var reference = _own.Load() ?? throw new TtsException(none, settingKey: SpeechCapability.CustomVoicesKey);

                try
                {
                    loaded.Own = engine.Encode(reference);
                    loaded.OwnVersion = version;
                }
                finally
                {
                    Array.Clear(reference);
                }
            }

            return Clip(text, engine.Speak(ids, loaded.Own, cancellationToken));
        }
    }

    /// <summary>A line in a custom voice, encoded from its clip decrypted into memory, once per version.</summary>
    private AudioClip SpeakCustom(string text, long[] ids, string id, Loaded loaded, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            var engine = Engine(loaded);
            var version = _custom!.Version(id);

            if (!loaded.Custom.TryGetValue(id, out var cached) || cached.Version != version)
            {
                if (cached.Encoded is not null)
                {
                    cached.Encoded.Dispose();
                    loaded.Custom.Remove(id);
                }

                var reference = _custom.Load(id)
                    ?? throw new TtsException($"The custom voice {id} is not saved or does not open on this PC.");

                try
                {
                    cached = (version, engine.Encode(reference));
                    loaded.Custom[id] = cached;
                }
                finally
                {
                    Array.Clear(reference);
                }
            }

            return Clip(text, engine.Speak(ids, cached.Encoded, cancellationToken));
        }
    }

    private void OnCustomVoicesChanged() => _ = Task.Run(DropCustom);

    private void DropCustom()
    {
        lock (_gate)
        {
            _turbo.DropCustom();
            _multilingual.DropCustom();
        }
    }

    /// <summary>Drops the encoder output for a recording that was replaced or deleted, off the caller's thread.</summary>
    private void OnOwnVoiceChanged() => _ = Task.Run(DropOwn);

    private void DropOwn()
    {
        lock (_gate)
        {
            _turbo.DropOwn();
            _multilingual.DropOwn();
        }
    }

    private static AudioClip Clip(string text, float[] samples) =>
        samples.Length == 0
            ? new AudioClip(text, ReadOnlyMemory<byte>.Empty, AudioFormat.Standard)
            : new AudioClip(text, PcmUpsample.Double(PcmConverter.FloatsToPcm16(samples)), AudioFormat.Standard);

    /// <summary>The engine of <paramref name="loaded"/>, opened the first time it speaks and kept until disposed. The caller holds <c>_gate</c>.</summary>
    private IChatterboxEngine Engine(Loaded loaded)
    {
        if (loaded.Engine is { } open)
        {
            return open;
        }

        var started = System.Diagnostics.Stopwatch.StartNew();
        var engine = loaded == _multilingual ? _japanese!.Open() : _open();

        _logger.LogInformation("{Model} is loaded ({Milliseconds} ms)", loaded.Name, started.ElapsedMilliseconds);
        return loaded.Engine = engine;
    }

    /// <summary>Under its own lock, so asking which tags are performed never waits behind a line being spoken.</summary>
    private ChatterboxTokeniser Tokeniser()
    {
        lock (_load)
        {
            return _tokeniser ??= ChatterboxTokeniser.Load(
                _files, ChatterboxAssets.Destination(_modelFolder, ChatterboxAssets.Tokenizer));
        }
    }

    private ChatterboxMultilingualTokeniser JapaneseTokeniser()
    {
        lock (_load)
        {
            return _japaneseTokeniser ??= ChatterboxMultilingualTokeniser.Load(
                _files, ChatterboxMultilingualAssets.Destination(_japanese!.Folder, ChatterboxMultilingualAssets.Tokenizer));
        }
    }

    private IReadOnlyList<ChatterboxVoice> Voices()
    {
        lock (_load)
        {
            return _voices ??= ChatterboxCatalog.Load(
                _files, _voicesFolder, _fetchedFolder, ChatterboxVoices.Load(_files, _voicesFolder, _logger), _logger);
        }
    }

    /// <summary>A listed voice by its exact id; nothing else is turned into a path or an address.</summary>
    private ChatterboxVoice? Find(string? voiceId) =>
        voiceId is { Length: > 0 }
            ? Voices().FirstOrDefault(v => string.Equals(v.Voice.Id, voiceId, StringComparison.Ordinal))
            : null;

    /// <summary>Whether a voice's clip can be spoken from, checking a fetched clip's hash once a session.</summary>
    private bool IsHere(ChatterboxVoice voice)
    {
        if (voice.Shipped)
        {
            return true;
        }

        lock (_clips)
        {
            if (_verified.Contains(voice.Voice.Id))
            {
                return true;
            }
        }

        if (ChatterboxCatalog.Here(_files, voice, _logger) is null)
        {
            return false;
        }

        lock (_clips)
        {
            _verified.Add(voice.Voice.Id);
        }

        return true;
    }

    /// <summary>The fetch of a voice's clip under way, started when there is none. Never faults.</summary>
    private Task<bool> Fetching(ChatterboxVoice voice)
    {
        Task<bool> started;

        lock (_clips)
        {
            if (_fetching.TryGetValue(voice.Voice.Id, out var running))
            {
                return running;
            }

            started = Task.Run(() => FetchClipAsync(voice));
            _fetching[voice.Voice.Id] = started;
        }

        ClipChanged(voice.Voice.Id);
        return started;
    }

    private async Task<bool> FetchClipAsync(ChatterboxVoice voice)
    {
        var id = voice.Voice.Id;
        var url = ChatterboxCatalog.Url(voice)!;

        try
        {
            if (IsHere(voice))
            {
                return true;
            }

            var clip = await _download(url, voice.Bytes, CancellationToken.None).ConfigureAwait(false);

            if (!ChatterboxCatalog.Matches(voice, clip))
            {
                throw new InvalidDataException("the clip that arrived does not match its size and SHA-256 in catalog.tsv");
            }

            _files.WriteBytes(voice.ClipPath, clip);

            lock (_clips)
            {
                _verified.Add(id);
                _failed.Remove(id);
            }

            _logger.LogInformation("Chatterbox voice {Id} fetched from {Url}", id, url);
            return true;
        }
        catch (Exception ex)
        {
            bool first;

            lock (_clips)
            {
                _failed[id] = ex.Message;
                first = _failureLogged.Add(id);
            }

            if (first)
            {
                _logger.LogWarning("Chatterbox voice {Id} could not be fetched from {Url}: {Reason}", id, url, ex.Message);
            }

            return false;
        }
        finally
        {
            lock (_clips)
            {
                _fetching.Remove(id);
            }

            ClipChanged(id);
        }
    }

    /// <summary>Raises <see cref="ClipStateChanged"/>; a handler that throws is logged and does not fault the fetch.</summary>
    private void ClipChanged(string id)
    {
        try
        {
            ClipStateChanged?.Invoke(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A handler of Chatterbox voice {Id}'s clip state threw", id);
        }
    }

    /// <summary>A shipped clip, already 24 kHz mono 16-bit, as floats.</summary>
    private static float[] Reference(IFileSystem files, string clipPath)
    {
        var pcm = WavReader.Read(files, clipPath).Pcm.Span;
        return PcmConverter.Pcm16ToFloats(pcm);
    }

    public void Dispose()
    {
        if (_own is not null)
        {
            _own.Changed -= OnOwnVoiceChanged;
        }

        if (_custom is not null)
        {
            _custom.Changed -= OnCustomVoicesChanged;
        }

        lock (_gate)
        {
            _disposed = true;
            _turbo.Dispose();
            _multilingual.Dispose();
        }

        _japanese?.Owned?.Dispose();
    }

    /// <summary>One engine, once open, and every voice it has encoded.</summary>
    private sealed class Loaded(string name) : IDisposable
    {
        public string Name { get; } = name;

        public IChatterboxEngine? Engine { get; set; }

        public Dictionary<string, IDisposable> Encoded { get; } = new(StringComparer.Ordinal);

        public IDisposable? Own { get; set; }

        public int OwnVersion { get; set; }

        public Dictionary<string, (int Version, IDisposable Encoded)> Custom { get; } = new(StringComparer.Ordinal);

        public bool HasCustom(string id, int version) => Custom.TryGetValue(id, out var cached) && cached.Version == version;

        public void DropOwn()
        {
            Own?.Dispose();
            Own = null;
        }

        public void DropCustom()
        {
            foreach (var cached in Custom.Values)
            {
                cached.Encoded.Dispose();
            }

            Custom.Clear();
        }

        public void Dispose()
        {
            DropOwn();
            DropCustom();

            foreach (var encoded in Encoded.Values)
            {
                encoded.Dispose();
            }

            Encoded.Clear();
            Engine?.Dispose();
            Engine = null;
        }
    }
}

/// <summary>
/// What a Japanese line needs: where Multilingual's tokenizer is, its engine, whether it and the dictionary are on this
/// PC, and the reader that splits a line into words.
/// </summary>
internal sealed record ChatterboxJapaneseSetup(
    string Folder,
    Func<IChatterboxEngine> Open,
    Func<bool> Installed,
    Func<string, IReadOnlyList<JapaneseWord>> Read)
{
    /// <summary>Disposed with the provider.</summary>
    public IDisposable? Owned { get; init; }

    /// <summary>Multilingual from <paramref name="folder"/>, read with the IPAdic files in <paramref name="dictionary"/>.</summary>
    public static ChatterboxJapaneseSetup OnThisMachine(IFileSystem files, string folder, string dictionary)
    {
        var reader = new JapaneseReader(files, dictionary);

        return new ChatterboxJapaneseSetup(
            folder,
            () => ChatterboxMultilingualPipeline.Open(folder, PerformanceCores.ForThisMachine()),
            () => ChatterboxMultilingualAssets.IsInstalled(files, folder) && JapaneseDictionaryAssets.IsInstalled(files, dictionary),
            reader.Read)
        {
            Owned = reader,
        };
    }
}
