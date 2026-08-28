using System.Security.Cryptography;
using System.Text.Json;
using D47.Core;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Stt;

/// <summary>
/// Fetching ggml speech models over HTTPS, on demand.
/// <para>
/// <b>Nothing here runs unless a model is selected.</b> Not on a timer, not on first listen, not
/// to "warm up" — every path into this class starts from the Commander's selection, including
/// <see cref="DescribeAsync"/>, which fetches the file's size and published hash so the download
/// that follows can be checked against something.
/// </para>
/// </summary>
public sealed class HttpModelStore : IModelStore, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpModelStore> _logger;

    public HttpModelStore(AppPaths paths, ILogger<HttpModelStore> logger)
    {
        _logger = logger;
        Directory = Path.Combine(paths.Data, "models");

        _http = new HttpClient
        {
            // A large model on a slow connection is a long transfer, not a hung one. The
            // per-read cancellation token is what actually bounds this.
            Timeout = Timeout.InfiniteTimeSpan,
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("D47");
    }

    public string Directory { get; }

    public bool IsInstalled(WhisperModel model) => File.Exists(Path.Combine(Directory, model.FileName));

    public string? PathOf(WhisperModel model)
    {
        var path = Path.Combine(Directory, model.FileName);
        return File.Exists(path) ? path : null;
    }

    public IReadOnlyList<string> Installed() =>
        [.. WhisperModels.All.Where(IsInstalled).Select(model => model.Id)];

    /// <summary>
    /// Asks the host what the file is. The size and hash come from the repository's own file
    /// listing rather than being written into d47: a size hardcoded here is a number d47
    /// asserts about a file it has never seen, and it goes stale the first time the model is
    /// republished.
    /// </summary>
    public async Task<ModelOffer?> DescribeAsync(
        WhisperModel model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http
                .GetAsync(WhisperModels.MetadataUrl(), cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            using var document = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("siblings", out var siblings) ||
                siblings.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var file in siblings.EnumerateArray())
            {
                if (file.TryGetProperty("rfilename", out var name) &&
                    name.ValueKind == JsonValueKind.String &&
                    name.GetString() == model.RepositoryPath)
                {
                    // Large files are stored via LFS and the block carries their SHA-256. A
                    // model small enough not to be LFS-backed simply has no hash here, which is
                    // reported rather than papered over.
                    var lfs = file.TryGetProperty("lfs", out var value) && value.ValueKind == JsonValueKind.Object
                        ? value
                        : (JsonElement?)null;

                    var size = lfs?.TryGetProperty("size", out var sizeValue) == true &&
                               sizeValue.TryGetInt64(out var bytes)
                        ? bytes
                        : 0;

                    // <b>The key is <c>sha256</c>.</b> This read <c>oid</c> alone, which this
                    // listing does not carry, so the hash was null on every model and the
                    // verification below never once ran. <c>oid</c> is kept as a second reading
                    // because it is what the LFS pointer itself calls the field and other
                    // shapes of this API have used it.
                    var sha =
                        (lfs?.TryGetProperty("sha256", out var hash) == true ? hash.GetString() : null)
                        ?? (lfs?.TryGetProperty("oid", out var oid) == true ? oid.GetString() : null);

                    // <b>The pin wins, and a disagreement is refused rather than resolved.</b>
                    // Falling back to the host's value on a mismatch would undo the whole point:
                    // the pin exists precisely so that the server cannot both change the file
                    // and change what the file is expected to be.
                    if (model.Sha256 is { Length: > 0 } pinned)
                    {
                        if (sha is { Length: > 0 } offered &&
                            !string.Equals(offered, pinned, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning(
                                "{Host} offers {Model} with hash {Offered}, which is not the {Pinned} d47 "
                                + "expects. The file has changed since it was pinned, or it is not the file.",
                                WhisperModels.Host,
                                model.Id,
                                offered,
                                pinned);
                        }

                        return new ModelOffer(model, size, pinned);
                    }

                    return new ModelOffer(model, size, sha);
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not ask {Host} about {Model}", WhisperModels.Host, model.Id);
            return null;
        }
    }

    public async Task<ModelInstallResult> InstallAsync(
        WhisperModel model,
        IProgress<ModelProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsInstalled(model))
        {
            return new ModelInstallResult(ModelInstall.AlreadyPresent);
        }

        var offer = await DescribeAsync(model, cancellationToken).ConfigureAwait(false);

        if (offer is null)
        {
            return new ModelInstallResult(
                ModelInstall.Failed,
                $"Could not reach {WhisperModels.Host} to ask about {model.Id}.");
        }

        System.IO.Directory.CreateDirectory(Directory);

        var destination = Path.Combine(Directory, model.FileName);

        // Written to a sibling and moved into place, the same shape as every other write d47
        // does. A half-downloaded model under its real name is a model that loads and then
        // fails in the middle of a transcription.
        var pending = destination + AtomicFileSuffix;

        try
        {
            _logger.LogInformation(
                "Downloading {Model} ({Megabytes:0.#} MB) from {Host}",
                model.Id,
                offer.Megabytes,
                WhisperModels.Host);

            string actualHash;

            using (var response = await _http
                       .GetAsync(offer.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? offer.Bytes;

                await using var source = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);

                await using var file = new FileStream(
                    pending, FileMode.Create, FileAccess.Write, FileShare.None);

                // Hashed as it lands rather than by re-reading the file afterwards: a second
                // pass over 500 MB to learn something the first pass already went past is
                // avoidable work on the slowest thing in the operation.
                using var sha = SHA256.Create();

                var buffer = new byte[81920];
                long received = 0;
                int read;

                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    sha.TransformBlock(buffer, 0, read, null, 0);

                    received += read;
                    progress?.Report(new ModelProgress(model.Id, received, total));
                }

                sha.TransformFinalBlock([], 0, 0);
                actualHash = Convert.ToHexStringLower(sha.Hash!);
            }

            if (offer.Sha256 is { } expected &&
                !string.Equals(actualHash, expected, StringComparison.OrdinalIgnoreCase))
            {
                // Discarded rather than kept. A model that does not match its published hash is
                // either a truncated transfer or something d47 should not be loading, and both
                // answers are "do not use this file".
                File.Delete(pending);

                _logger.LogError(
                    "{Model} did not match the published checksum; the download was discarded", model.Id);

                return new ModelInstallResult(
                    ModelInstall.ChecksumMismatch,
                    "The downloaded model did not match the checksum the host published, so it was discarded.");
            }

            File.Move(pending, destination, overwrite: true);

            _logger.LogInformation("{Model} installed to {Path}", model.Id, destination);
            return new ModelInstallResult(ModelInstall.Installed);
        }
        catch (Exception ex)
        {
            TryDelete(pending);

            if (ex is OperationCanceledException)
            {
                _logger.LogInformation("Download of {Model} was cancelled", model.Id);
                return new ModelInstallResult(ModelInstall.Cancelled, "The download was cancelled.");
            }

            _logger.LogError(ex, "Could not download {Model}", model.Id);
            return new ModelInstallResult(ModelInstall.Failed, ex.Message);
        }
    }

    public bool Remove(WhisperModel model)
    {
        var path = Path.Combine(Directory, model.FileName);

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            File.Delete(path);
            _logger.LogInformation("Removed {Model}", model.Id);
            return true;
        }
        catch (IOException ex)
        {
            // Usually the model being loaded right now.
            _logger.LogWarning(ex, "Could not remove {Model}", model.Id);
            return false;
        }
    }

    /// <summary>The same suffix the settings and secret stores use for a pending write.</summary>
    private const string AtomicFileSuffix = ".writing";

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A leftover partial download is not worth reporting over whatever actually failed.
        }
    }

    public void Dispose() => _http.Dispose();
}
