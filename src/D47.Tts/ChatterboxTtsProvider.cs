using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>
/// Chatterbox Turbo, run on this machine's CPU from a reference clip: one that ships beside the exe, or one fetched
/// from the voice release the first time it is picked, previewed or spoken.
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
    private readonly Dictionary<string, IDisposable> _encoded = new(StringComparer.Ordinal);
    private readonly OwnVoice? _own;
    private readonly CustomVoices? _custom;
    private readonly Dictionary<string, (int Version, IDisposable Encoded)> _customEncoded = new(StringComparer.Ordinal);

    /// <summary>Guards the sets below.</summary>
    private readonly Lock _clips = new();
    private readonly HashSet<string> _verified = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<bool>> _fetching = new(StringComparer.Ordinal);

    /// <summary>Voices whose fetch failed this session; a line does not ask again, a pick or a preview does.</summary>
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failureLogged = new(StringComparer.Ordinal);
    private readonly HashSet<string> _standInLogged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CustomVoice> _customKnown = new(StringComparer.Ordinal);

    private IChatterboxEngine? _engine;
    private ChatterboxTokeniser? _tokeniser;
    private IReadOnlyList<ChatterboxVoice>? _voices;
    private IDisposable? _ownEncoded;
    private int _ownVersion;
    private bool _disposed;

    /// <param name="modelFolder">Where <see cref="ChatterboxInstaller"/> put the graphs and tokenizer.</param>
    /// <param name="voicesFolder">Where <c>voices.tsv</c>, <c>catalog.tsv</c> and the shipped clips are.</param>
    /// <param name="fetchedFolder">Where fetched clips are kept.</param>
    /// <param name="own">The Commander's own recording, listed as <see cref="OwnVoice.VoiceId"/> while one is saved.</param>
    /// <param name="custom">The Commander's custom voices, listed after the catalogue.</param>
    public ChatterboxTtsProvider(
        IFileSystem files,
        string modelFolder,
        string voicesFolder,
        string fetchedFolder,
        ILogger<ChatterboxTtsProvider> logger,
        OwnVoice? own = null,
        CustomVoices? custom = null)
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
            custom)
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
        CustomVoices? custom = null)
    {
        _files = files;
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
        };

    /// <summary>Every voice with its pitch and pace bands, whether or not its clip is on this machine.</summary>
    public IReadOnlyList<ChatterboxVoice> Catalogue() => Voices();

    /// <summary>How long a line waits for a clip being fetched before it is spoken in a stand-in.</summary>
    internal TimeSpan FirstLineWait { get; init; } = TimeSpan.FromSeconds(3);

    public Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            async () => Speak(text, await SpokenAsync(voice, cancellationToken).ConfigureAwait(false), cancellationToken),
            cancellationToken);

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
                settingKey: SpeechCapability.ChatterboxVoiceKey);
        }

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
            failed = _failed.Contains(chosen.Voice.Id);
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
            ?? throw new TtsException(
                $"The Chatterbox voice {chosen.Voice.Name} is not on this PC yet, and no shipped voice can stand in.");

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
            return _customEncoded.TryGetValue(id, out var cached) && cached.Version == _custom!.Version(id);
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

    private AudioClip Speak(string text, ChatterboxVoice? chosen, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (chosen is null)
        {
            return SpeakOwn(text, cancellationToken);
        }

        if (chosen.Voice.Custom)
        {
            return SpeakCustom(text, chosen.Voice.Id, cancellationToken);
        }

        // One line at a time: each already takes every performance core.
        var ids = Tokeniser().Encode(text);

        lock (_gate)
        {
            ThrowIfDisposed();

            var engine = _engine ??= Open();

            if (!_encoded.TryGetValue(chosen.Voice.Id, out var encoded))
            {
                encoded = engine.Encode(Reference(_files, chosen.ClipPath));
                _encoded[chosen.Voice.Id] = encoded;
            }

            return Clip(text, engine.Speak(ids, encoded, cancellationToken));
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, nameof(ChatterboxTtsProvider));

    /// <summary>A line in the Commander's recorded voice, encoded from the recording decrypted into memory.</summary>
    private AudioClip SpeakOwn(string text, CancellationToken cancellationToken)
    {
        const string none = "No recording of your voice is saved. Record one in Settings, under Your voice.";

        if (_own is null || !_own.Exists)
        {
            throw new TtsException(none, settingKey: SpeechCapability.OwnVoiceKey);
        }

        var ids = Tokeniser().Encode(text);

        lock (_gate)
        {
            ThrowIfDisposed();

            var engine = _engine ??= Open();
            var version = _own.Version;

            if (_ownEncoded is null || _ownVersion != version)
            {
                _ownEncoded?.Dispose();
                _ownEncoded = null;

                var reference = _own.Load() ?? throw new TtsException(none, settingKey: SpeechCapability.OwnVoiceKey);

                try
                {
                    _ownEncoded = engine.Encode(reference);
                    _ownVersion = version;
                }
                finally
                {
                    Array.Clear(reference);
                }
            }

            return Clip(text, engine.Speak(ids, _ownEncoded, cancellationToken));
        }
    }

    /// <summary>A line in a custom voice, encoded from its clip decrypted into memory, once per version.</summary>
    private AudioClip SpeakCustom(string text, string id, CancellationToken cancellationToken)
    {
        var ids = Tokeniser().Encode(text);

        lock (_gate)
        {
            ThrowIfDisposed();

            var engine = _engine ??= Open();
            var version = _custom!.Version(id);

            if (!_customEncoded.TryGetValue(id, out var cached) || cached.Version != version)
            {
                if (cached.Encoded is not null)
                {
                    cached.Encoded.Dispose();
                    _customEncoded.Remove(id);
                }

                var reference = _custom.Load(id)
                    ?? throw new TtsException($"The custom voice {id} is not saved or does not open on this PC.");

                try
                {
                    cached = (version, engine.Encode(reference));
                    _customEncoded[id] = cached;
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
            foreach (var cached in _customEncoded.Values)
            {
                cached.Encoded.Dispose();
            }

            _customEncoded.Clear();
        }
    }

    /// <summary>Drops the encoder output for a recording that was replaced or deleted, off the caller's thread.</summary>
    private void OnOwnVoiceChanged() => _ = Task.Run(DropOwn);

    private void DropOwn()
    {
        lock (_gate)
        {
            _ownEncoded?.Dispose();
            _ownEncoded = null;
        }
    }

    private static AudioClip Clip(string text, float[] samples) =>
        samples.Length == 0
            ? new AudioClip(text, ReadOnlyMemory<byte>.Empty, AudioFormat.Standard)
            : new AudioClip(text, PcmUpsample.Double(ToPcm(samples)), AudioFormat.Standard);

    private IChatterboxEngine Open()
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var engine = _open();

        _logger.LogInformation("Chatterbox is loaded ({Milliseconds} ms)", started.ElapsedMilliseconds);
        return engine;
    }

    /// <summary>Under its own lock, so asking which tags are performed never waits behind a line being spoken.</summary>
    private ChatterboxTokeniser Tokeniser()
    {
        lock (_load)
        {
            return _tokeniser ??= ChatterboxTokeniser.Load(
                ChatterboxAssets.Destination(_modelFolder, ChatterboxAssets.Tokenizer));
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
        lock (_clips)
        {
            if (_fetching.TryGetValue(voice.Voice.Id, out var running))
            {
                return running;
            }

            var started = Task.Run(() => FetchClipAsync(voice));
            _fetching[voice.Voice.Id] = started;
            return started;
        }
    }

    private async Task<bool> FetchClipAsync(ChatterboxVoice voice)
    {
        var id = voice.Voice.Id;
        var url = ChatterboxCatalog.Url(voice)!;
        var partial = voice.ClipPath + ".part";

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

            Directory.CreateDirectory(Path.GetDirectoryName(voice.ClipPath)!);
            await File.WriteAllBytesAsync(partial, clip).ConfigureAwait(false);
            File.Move(partial, voice.ClipPath, overwrite: true);

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
                _failed.Add(id);
                first = _failureLogged.Add(id);
            }

            if (first)
            {
                _logger.LogWarning("Chatterbox voice {Id} could not be fetched from {Url}: {Reason}", id, url, ex.Message);
            }

            try
            {
                File.Delete(partial);
            }
            catch (Exception)
            {
                // A leftover .part is never read.
            }

            return false;
        }
        finally
        {
            lock (_clips)
            {
                _fetching.Remove(id);
            }
        }
    }

    /// <summary>A shipped clip, already 24 kHz mono 16-bit, as floats.</summary>
    private static float[] Reference(IFileSystem files, string clipPath)
    {
        var pcm = WavReader.Read(files, clipPath).Pcm.Span;
        var samples = new float[pcm.Length / 2];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(pcm[i * 2] | (pcm[(i * 2) + 1] << 8)) / 32768f;
        }

        return samples;
    }

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
            _ownEncoded?.Dispose();
            _ownEncoded = null;

            foreach (var cached in _customEncoded.Values)
            {
                cached.Encoded.Dispose();
            }

            _customEncoded.Clear();

            foreach (var encoded in _encoded.Values)
            {
                encoded.Dispose();
            }

            _encoded.Clear();
            _engine?.Dispose();
            _engine = null;
        }
    }
}
