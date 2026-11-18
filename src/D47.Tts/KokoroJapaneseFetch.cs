using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>Fetches a Kokoro Japanese voice and the dictionary it reads Japanese with, on the pool, each file once.</summary>
public sealed class KokoroJapaneseFetch : IDisposable
{
    private readonly IFileSystem _files;
    private readonly string _kokoroFolder;
    private readonly ILogger<KokoroJapaneseFetch> _logger;
    private readonly HttpClient _http = ModelDownload.CreateClient();
    private readonly JapaneseDictionaryInstaller _dictionary;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Task> _fetching = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

    private Task<KokoroInstallResult>? _dictionaryFetch;

    /// <param name="kokoroFolder">The local voice's folder; a voice lands in its <c>voices</c> folder.</param>
    /// <param name="dictionaryFolder">Where the IPAdic files land.</param>
    public KokoroJapaneseFetch(IFileSystem files, string kokoroFolder, string dictionaryFolder, ILoggerFactory loggers)
    {
        ArgumentNullException.ThrowIfNull(loggers);

        _files = files;
        _kokoroFolder = kokoroFolder;
        _logger = loggers.CreateLogger<KokoroJapaneseFetch>();
        _dictionary = new JapaneseDictionaryInstaller(files, dictionaryFolder, loggers.CreateLogger<JapaneseDictionaryInstaller>());
    }

    /// <summary>
    /// Starts fetching whatever <paramref name="voiceId"/> needs that is not here. Does nothing while a fetch for it runs,
    /// or, unless <paramref name="retry"/>, after its last fetch failed. Never waits.
    /// </summary>
    public void Start(string voiceId, bool retry = false)
    {
        if (KokoroAssets.JapaneseVoice(voiceId) is not { } voice)
        {
            return;
        }

        lock (_gate)
        {
            if (retry)
            {
                _failed.Remove(voiceId);
            }

            if (_failed.Contains(voiceId) || _fetching.ContainsKey(voiceId))
            {
                return;
            }

            _fetching[voiceId] = Task.Run(() => FetchAsync(voiceId, voice));
        }
    }

    private async Task FetchAsync(string voiceId, KokoroAsset voice)
    {
        try
        {
            var result = await Dictionary().ConfigureAwait(false);

            if (result.Outcome is KokoroInstall.Installed or KokoroInstall.AlreadyPresent)
            {
                result = await VoiceAsync(voice).ConfigureAwait(false);
            }

            if (result.Outcome is KokoroInstall.Installed or KokoroInstall.AlreadyPresent)
            {
                _logger.LogInformation("Kokoro's Japanese voice {Voice} and the Japanese dictionary are on this PC", voiceId);
                return;
            }

            Failed(voiceId, result.Detail ?? result.Outcome.ToString());
        }
        catch (Exception ex)
        {
            Failed(voiceId, ex.Message);
        }
        finally
        {
            lock (_gate)
            {
                _fetching.Remove(voiceId);
            }
        }
    }

    private void Failed(string voiceId, string reason)
    {
        lock (_gate)
        {
            _failed.Add(voiceId);
        }

        _logger.LogWarning("Kokoro's Japanese voice {Voice} could not be fetched: {Reason}", voiceId, reason);
    }

    /// <summary>The dictionary fetch running, or a new one where none is.</summary>
    private Task<KokoroInstallResult> Dictionary()
    {
        lock (_gate)
        {
            if (_dictionaryFetch is not { IsCompleted: false })
            {
                _dictionaryFetch = _dictionary.InstallAsync();
            }

            return _dictionaryFetch;
        }
    }

    private Task<KokoroInstallResult> VoiceAsync(KokoroAsset voice)
    {
        var destination = Path.Combine(_kokoroFolder, "voices", Path.GetFileName(voice.Path));

        return _files.Stat(destination) is { } present && present.Length == voice.Bytes
            ? Task.FromResult(new KokoroInstallResult(KokoroInstall.AlreadyPresent))
            : ModelDownload.FetchAsync(_http, _logger, voice, destination, 0, voice.Bytes, null, CancellationToken.None);
    }

    public void Dispose()
    {
        _http.Dispose();
        _dictionary.Dispose();
    }
}
