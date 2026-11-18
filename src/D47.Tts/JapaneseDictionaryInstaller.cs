using D47.Core.Speech;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>Fetches the IPAdic files <see cref="JapaneseReader"/> reads with, once.</summary>
public sealed class JapaneseDictionaryInstaller : IDisposable
{
    private readonly IFileSystem _files;
    private readonly string _folder;
    private readonly HttpClient _http;
    private readonly ILogger<JapaneseDictionaryInstaller> _logger;

    public JapaneseDictionaryInstaller(IFileSystem files, string folder, ILogger<JapaneseDictionaryInstaller> logger)
    {
        _files = files;
        _folder = folder;
        _logger = logger;
        _http = ModelDownload.CreateClient();
    }

    public bool IsInstalled => JapaneseDictionaryAssets.IsInstalled(_files, _folder);

    /// <summary>Every file not already present at its pinned size; a file whose hash does not match is discarded.</summary>
    public async Task<KokoroInstallResult> InstallAsync(
        IProgress<KokoroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsInstalled)
        {
            return new KokoroInstallResult(KokoroInstall.AlreadyPresent);
        }

        var total = JapaneseDictionaryAssets.All.Sum(asset => asset.Bytes);
        long done = 0;

        foreach (var asset in JapaneseDictionaryAssets.All)
        {
            var destination = JapaneseDictionaryAssets.Destination(_folder, asset);

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

        _logger.LogInformation("The Japanese dictionary is installed in {Folder}", _folder);
        return new KokoroInstallResult(KokoroInstall.Installed);
    }

    public void Dispose() => _http.Dispose();
}
