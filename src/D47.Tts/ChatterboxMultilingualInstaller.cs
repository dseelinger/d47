using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>Fetches Chatterbox Multilingual's graphs and tokenizer into a folder of their own, once.</summary>
public sealed class ChatterboxMultilingualInstaller : IDisposable
{
    private readonly IFileSystem _files;
    private readonly string _folder;
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly ILogger<ChatterboxMultilingualInstaller> _logger;

    public ChatterboxMultilingualInstaller(IFileSystem files, string folder, ILogger<ChatterboxMultilingualInstaller> logger)
        : this(files, folder, logger, ModelDownload.CreateClient(), ownsClient: true)
    {
    }

    internal ChatterboxMultilingualInstaller(
        IFileSystem files, string folder, ILogger<ChatterboxMultilingualInstaller> logger, HttpClient http, bool ownsClient = false)
    {
        _files = files;
        _folder = folder;
        _logger = logger;
        _http = http;
        _ownsClient = ownsClient;
    }

    public bool IsInstalled => ChatterboxMultilingualAssets.IsInstalled(_files, _folder);

    /// <summary>
    /// Every file not already present at its pinned size, reporting against the whole set. A file whose size or SHA-256
    /// does not match is discarded and the install stops there.
    /// </summary>
    public async Task<KokoroInstallResult> InstallAsync(
        IProgress<KokoroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsInstalled)
        {
            return new KokoroInstallResult(KokoroInstall.AlreadyPresent);
        }

        var total = ChatterboxMultilingualAssets.All.Sum(asset => asset.Bytes);
        long done = 0;

        foreach (var asset in ChatterboxMultilingualAssets.All)
        {
            var destination = ChatterboxMultilingualAssets.Destination(_folder, asset);

            if (_files.Stat(destination) is not { } present || present.Length != asset.Bytes)
            {
                var result = await ModelDownload
                    .FetchAsync(_http, _logger, asset, destination, done, total, progress, cancellationToken)
                    .ConfigureAwait(false);

                if (result.Outcome != KokoroInstall.Installed)
                {
                    return result;
                }
            }

            done += asset.Bytes;
            progress?.Report(new KokoroProgress(asset.Path, done, total));
        }

        _logger.LogInformation("Chatterbox Multilingual is installed in {Folder}", _folder);
        return new KokoroInstallResult(KokoroInstall.Installed);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}
