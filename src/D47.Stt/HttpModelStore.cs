using System.Security.Cryptography;
using System.Text.Json;
using D47.Core;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.Stt;

/// <summary>
/// Fetching ggml speech models over HTTPS, on demand and only with consent.
/// <para>
/// <b>Nothing here runs unless the Commander asked for a model.</b> Not at startup, not on first
/// listen, not to "warm up". The only network call that can happen without consent is
/// <see cref="DescribeAsync"/>, which fetches the file's size and hash so the consent question
/// can state facts — and even that only runs because somebody selected a model.
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

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47");
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
                    // Large files are stored via LFS, and the oid is their SHA-256. A model
                    // small enough not to be LFS-backed simply has no hash here, which is
                    // reported rather than papered over.
                    var lfs = file.TryGetProperty("lfs", out var value) && value.ValueKind == JsonValueKind.Object
                        ? value
                        : (JsonElement?)null;

                    var size = lfs?.TryGetProperty("size", out var sizeValue) == true &&
                               sizeValue.TryGetInt64(out var bytes)
                        ? bytes
                        : 0;

                    var sha = lfs?.TryGetProperty("oid", out var oid) == true ? oid.GetString() : null;

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
        Func<ModelOffer, Task<bool>> consent,
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

        // The gate. Everything above this line is metadata; everything below transfers
        // hundreds of megabytes.
        if (!await consent(offer).ConfigureAwait(false))
        {
            _logger.LogInformation("Download of {Model} declined; nothing was fetched", model.Id);
            return new ModelInstallResult(ModelInstall.Declined);
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
                return new ModelInstallResult(ModelInstall.Declined, "The download was cancelled.");
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
