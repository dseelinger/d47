using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>Chatterbox Turbo, run on this machine's CPU from the shipped reference clips.</summary>
public sealed class ChatterboxTtsProvider : ITtsProvider, IDisposable
{
    public const string ProviderId = TtsProviderCatalog.ChatterboxId;

    private readonly string _modelFolder;
    private readonly string _voicesFolder;
    private readonly ILogger<ChatterboxTtsProvider> _logger;
    private readonly Func<IChatterboxEngine> _open;
    private readonly Func<bool> _installed;
    private readonly Lock _gate = new();
    private readonly Lock _load = new();
    private readonly Dictionary<string, IDisposable> _encoded = new(StringComparer.Ordinal);

    private IChatterboxEngine? _engine;
    private ChatterboxTokeniser? _tokeniser;
    private IReadOnlyList<ChatterboxVoice>? _voices;

    /// <param name="modelFolder">Where <see cref="ChatterboxInstaller"/> put the graphs and tokenizer.</param>
    /// <param name="voicesFolder">Where <c>voices.tsv</c> and the reference clips are.</param>
    public ChatterboxTtsProvider(string modelFolder, string voicesFolder, ILogger<ChatterboxTtsProvider> logger)
        : this(
            modelFolder,
            voicesFolder,
            logger,
            () => ChatterboxPipeline.Open(modelFolder, PerformanceCores.ForThisMachine()),
            () => ChatterboxAssets.IsInstalled(modelFolder))
    {
    }

    internal ChatterboxTtsProvider(
        string modelFolder,
        string voicesFolder,
        ILogger<ChatterboxTtsProvider> logger,
        Func<IChatterboxEngine> open,
        Func<bool> installed)
    {
        _modelFolder = modelFolder;
        _voicesFolder = voicesFolder;
        _logger = logger;
        _open = open;
        _installed = installed;
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

        return Task.FromResult(VoiceCatalogue.Of([.. Voices().Select(voice => voice.Voice)]));
    }

    public Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Speak(text, voice, cancellationToken), cancellationToken);

    /// <summary>The paralinguistic tags in <c>tokenizer.json</c>'s <c>added_tokens</c>; none before the download.</summary>
    public bool Performs(string tag)
    {
        if (!_installed())
        {
            return false;
        }

        return Tokeniser().Performs(tag);
    }

    private AudioClip Speak(string text, VoiceSelection voice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(voice);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_installed())
        {
            throw new TtsException("Chatterbox is not downloaded yet. Download it in Settings.");
        }

        var chosen = Voices().FirstOrDefault(v => string.Equals(v.Voice.Id, voice.VoiceId, StringComparison.Ordinal))
            ?? throw new TtsException(voice.VoiceId is { Length: > 0 } unknown
                ? $"Chatterbox has no voice called {unknown}. Pick one in Settings."
                : "No Chatterbox voice has been chosen. Pick one in Settings.");

        // One line at a time: each already takes every performance core.
        var ids = Tokeniser().Encode(text);

        lock (_gate)
        {
            var engine = _engine ??= Open();

            if (!_encoded.TryGetValue(chosen.Voice.Id, out var encoded))
            {
                encoded = engine.Encode(Reference(chosen.ClipPath));
                _encoded[chosen.Voice.Id] = encoded;
            }

            var samples = engine.Speak(ids, encoded, cancellationToken);

            if (samples.Length == 0)
            {
                return new AudioClip(text, ReadOnlyMemory<byte>.Empty, AudioFormat.Standard);
            }

            return new AudioClip(text, PcmUpsample.Double(ToPcm(samples)), AudioFormat.Standard);
        }
    }

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
            return _voices ??= ChatterboxVoices.Load(_voicesFolder, _logger);
        }
    }

    /// <summary>A shipped clip, already 24 kHz mono 16-bit, as floats.</summary>
    private static float[] Reference(string clipPath)
    {
        var pcm = WavReader.Read(clipPath).Pcm.Span;
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
        lock (_gate)
        {
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
