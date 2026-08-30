using System.Net;
using System.Net.Http.Headers;
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

    /// <summary>
    /// How much one worker asks for at a time. Small enough that the throttle measured on
    /// 2026-08-30 has not arrived by the end of it — 100 MB held 19 MB/s where 300 MB fell to
    /// 4.5 — and small enough that a short final chunk costs one worker a moment rather than
    /// leaving three idle through a last long round.
    /// </summary>
    private const long ChunkBytes = 64L * 1024 * 1024;

    /// <summary>How many chunks are in flight at once. See <see cref="FetchAsync"/> for why four.</summary>
    private const int Parallelism = 4;

    /// <summary>
    /// The read buffer, and the one the second pass hashes through. 256 KB rather than 80 KB;
    /// measured as neutral for throughput, and it is what makes one progress report per whole
    /// percent land naturally rather than needing a timer.
    /// </summary>
    private const int BufferBytes = 262144;

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

            var actualHash = await FetchAsync(model, offer, pending, progress, cancellationToken)
                .ConfigureAwait(false);

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

    /// <summary>
    /// Pulls the file down and answers its SHA-256.
    /// <para>
    /// <b>Several short requests at once, because this host throttles a connection rather than a
    /// caller.</b> Measured against it on 2026-08-30, on a gigabit line, with curl discarding to
    /// null so neither d47 nor the disk was in the way: the whole 465 MB model on <b>one</b>
    /// stream averaged 4.1 MB/s and took 119 s, while the <i>first</i> 100 MB of the same file
    /// ran at 24 MB/s and bytes 300-400 MB ran at 19 MB/s. So neither the line nor the offset is
    /// the limit — length of connection is. Ranges in parallel then scale almost cleanly:
    /// </para>
    /// <list type="table">
    /// <item><description>one at a time — about 17 MB/s</description></item>
    /// <item><description>two — 38 MB/s</description></item>
    /// <item><description>four — 58 MB/s</description></item>
    /// <item><description>eight — 84 MB/s</description></item>
    /// </list>
    /// <para>
    /// <b>Four is taken and eight is left, deliberately.</b> Eight is measurably faster and these
    /// files are served free; four already turns the largest model from three and a half minutes
    /// into well under one, and the rest is worth less than being a good guest. It is one
    /// constant if that judgement changes.
    /// </para>
    /// <para>
    /// <b>Chunks stay small for the same reason the whole file could not be one request.</b> 300
    /// MB in a single range measured 4.5 MB/s where 100 MB measured 19: the throttle arrives
    /// during a transfer, so a worker that takes a huge slice ends up in exactly the state this
    /// is avoiding. Workers pull 64 MB at a time from a shared cursor, which also balances the
    /// tail — the last chunk being short costs one worker a moment, not the whole download a
    /// round.
    /// </para>
    /// </summary>
    private async Task<string> FetchAsync(
        WhisperModel model,
        ModelOffer offer,
        string pending,
        IProgress<ModelProgress>? progress,
        CancellationToken cancellationToken)
    {
        var total = offer.Bytes;

        // Asked before anything is written, and it is one byte. A host that answers 200 to this
        // is one that would answer 200 to every worker as well — which would be the whole file
        // downloaded four times over, on top of itself.
        var ranged = total > 0 && await SupportsRangesAsync(offer.Url, cancellationToken).ConfigureAwait(false);

        if (!ranged)
        {
            _logger.LogInformation(
                "{Host} did not offer range requests for {Model}; downloading it in one stream",
                WhisperModels.Host,
                model.Id);
        }

        long received = 0;
        var lastPercent = -1;
        var reportLock = new object();

        void Advance(int bytes)
        {
            var now = Interlocked.Add(ref received, bytes);

            if (total <= 0)
            {
                return;
            }

            // Locked rather than raced: four workers crossing a percentage boundary together
            // would otherwise send four reports for it, and the point of the whole-percent gate
            // is that the dispatcher gets about a hundred posts rather than thousands (#84).
            var percent = (int)(now * 100 / total);

            lock (reportLock)
            {
                if (percent == lastPercent)
                {
                    return;
                }

                lastPercent = percent;
            }

            progress?.Report(new ModelProgress(model.Id, now, total));
        }

        // Written to a preallocated file at absolute offsets, so the workers never contend for a
        // position and the chunks may land in any order.
        using (var handle = File.OpenHandle(
                   pending,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.ReadWrite,
                   FileOptions.Asynchronous))
        {
            if (ranged)
            {
                RandomAccess.SetLength(handle, total);

                var next = 0L;

                async Task WorkAsync()
                {
                    while (true)
                    {
                        var start = Interlocked.Add(ref next, ChunkBytes) - ChunkBytes;

                        if (start >= total)
                        {
                            return;
                        }

                        var last = Math.Min(start + ChunkBytes, total) - 1;

                        using var request = new HttpRequestMessage(HttpMethod.Get, offer.Url);
                        request.Headers.Range = new RangeHeaderValue(start, last);

                        using var response = await _http
                            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                            .ConfigureAwait(false);

                        response.EnsureSuccessStatusCode();

                        if (response.StatusCode != HttpStatusCode.PartialContent)
                        {
                            // The probe said ranges were honoured and this one was not, so the
                            // file on disk can no longer be reasoned about. Louder than a
                            // fallback: a silent one would write a whole file over a slice.
                            throw new IOException(
                                $"{WhisperModels.Host} stopped honouring range requests part way through {model.Id}.");
                        }

                        await using var source = await response.Content
                            .ReadAsStreamAsync(cancellationToken)
                            .ConfigureAwait(false);

                        var buffer = new byte[BufferBytes];
                        var at = start;
                        int read;

                        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await RandomAccess
                                .WriteAsync(handle, buffer.AsMemory(0, read), at, cancellationToken)
                                .ConfigureAwait(false);

                            at += read;
                            Advance(read);
                        }
                    }
                }

                await Task.WhenAll(Enumerable.Range(0, Parallelism).Select(_ => WorkAsync()))
                    .ConfigureAwait(false);
            }
            else
            {
                using var response = await _http
                    .GetAsync(offer.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                await using var source = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);

                var buffer = new byte[BufferBytes];
                var at = 0L;
                int read;

                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await RandomAccess
                        .WriteAsync(handle, buffer.AsMemory(0, read), at, cancellationToken)
                        .ConfigureAwait(false);

                    at += read;
                    Advance(read);
                }

                // Whatever actually arrived, rather than what the catalogue said would.
                RandomAccess.SetLength(handle, at);
            }
        }

        // The last one always lands, whatever the arithmetic above did with it: a bar left at
        // 99% on a finished download is the one report worth spending.
        progress?.Report(new ModelProgress(model.Id, Interlocked.Read(ref received), total));

        // <b>Hashed by re-reading, where it used to be hashed as it landed.</b> That was the
        // right call when the bytes arrived at 4 MB/s and a second pass was pure waste; chunks
        // now land out of order, so there is no "as it lands" to hash in. The cost was measured
        // rather than assumed — a second pass over 1.5 GB off an SSD is about a second against
        // the two and a half minutes the parallel fetch saves on that same file.
        await using var written = new FileStream(
            pending, FileMode.Open, FileAccess.Read, FileShare.None, BufferBytes, FileOptions.Asynchronous);

        using var sha = SHA256.Create();

        return Convert.ToHexStringLower(
            await sha.ComputeHashAsync(written, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Whether the host will serve a slice, asked with a one-byte request rather than assumed.
    /// A <c>HEAD</c> would be politer still and is not used: this host answers it without the
    /// <c>Accept-Ranges</c> header, so the only reliable question is the one being asked for real.
    /// </summary>
    private async Task<bool> SupportsRangesAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, 0);

            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return response.StatusCode == HttpStatusCode.PartialContent;
        }
        catch (HttpRequestException)
        {
            // Not fatal and not the question being asked: the download itself is about to try
            // the network again and will report properly if it is really unreachable.
            return false;
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
