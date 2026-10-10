using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>Fetches Chatterbox Turbo's q4 graphs and tokenizer, once.</summary>
public sealed class ChatterboxInstaller : IDisposable
{
    private readonly IFileSystem _files;
    private readonly string _folder;
    private readonly HttpClient _http;
    private readonly ILogger<ChatterboxInstaller> _logger;

    public ChatterboxInstaller(IFileSystem files, string folder, ILogger<ChatterboxInstaller> logger)
    {
        _files = files;
        _folder = folder;
        _logger = logger;
        _http = ModelDownload.CreateClient();
    }

    public string Folder => _folder;

    public bool IsInstalled => ChatterboxAssets.IsInstalled(_files, _folder);

    /// <summary>Every file not already present at its pinned size, reporting against the whole set.</summary>
    public async Task<KokoroInstallResult> InstallAsync(
        IProgress<KokoroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsInstalled)
        {
            return new KokoroInstallResult(KokoroInstall.AlreadyPresent);
        }

        var total = ChatterboxAssets.All.Sum(asset => asset.Bytes);
        long done = 0;

        foreach (var asset in ChatterboxAssets.All)
        {
            var destination = ChatterboxAssets.Destination(_folder, asset);

            if (new FileInfo(destination) is not { Exists: true } present || present.Length != asset.Bytes)
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

        _logger.LogInformation("Chatterbox is installed in {Folder}", _folder);
        return new KokoroInstallResult(KokoroInstall.Installed);
    }

    public void Dispose() => _http.Dispose();
}
