using System.Security.Cryptography;
using D47.Core.Speech;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>One pinned model file, streamed to a <c>.part</c> file, hashed, and moved into place only when it matches.</summary>
internal static class ModelDownload
{
    private const string PendingSuffix = ".part";

    /// <summary>A client for multi-hundred-megabyte transfers: no timeout, and d47's user agent.</summary>
    public static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("D47");
        return http;
    }

    public static async Task<KokoroInstallResult> FetchAsync(
        HttpClient http,
        ILogger logger,
        KokoroAsset asset,
        string destination,
        long already,
        long total,
        IProgress<KokoroProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var pending = destination + PendingSuffix;

        try
        {
            string actual;

            // Scoped, and it has to be. `await using var` disposes at the end of the enclosing block,
            // so a File.Move written after it inside the same try runs while the stream is still open and
            // fails with "used by another process" -- which reads like a download problem and is not one.
            using (var response = await http
                       .GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                await using var source = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Asynchronous and unbuffered, for the reason HttpModelStore records: the four-argument
                // constructor defaults to blocking 4 KB writes, which is the wrong shape on a machine that is
                // busy even where it was measured as neutral on one that is not.
                await using var file = new FileStream(
                    pending, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 1, FileOptions.Asynchronous);

                using var sha = SHA256.Create();

                var buffer = new byte[262144];
                long received = 0;
                int read;
                var lastPercent = -1;

                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    received += read;

                    // Reported on whole percentage points of the WHOLE set, not of this file: a Commander
                    // watching a bar wants to know how far the download has got, and a bar that fills
                    // twenty-nine times tells them nothing.
                    var percent = total > 0 ? (int)((already + received) * 100 / total) : -1;

                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress?.Report(new KokoroProgress(asset.Path, already + received, total));
                    }
                }

                sha.TransformFinalBlock([], 0, 0);

                actual = Convert.ToHexStringLower(sha.Hash!);
            }

            // A file with no pinned hash is allowed: Kokoro's tokenizer is not stored through LFS, so the
            // listing carries no hash for it to be checked against.
            if (asset.Sha256 is { Length: > 0 } expected &&
                !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(pending);

                logger.LogError(
                    "{File} did not match the checksum D47 expects, so it was discarded", asset.Path);

                return new KokoroInstallResult(
                    KokoroInstall.ChecksumMismatch,
                    $"{asset.Path} is not the file D47 expects, so it was discarded.");
            }

            File.Move(pending, destination, overwrite: true);
            return new KokoroInstallResult(KokoroInstall.Installed);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            TryDelete(pending);

            logger.LogWarning(ex, "Could not download {File}", asset.Path);

            return new KokoroInstallResult(
                KokoroInstall.Failed, $"Could not download {asset.Path}: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        // A leftover .part is harmless: it is never loaded and the next run overwrites it.
        }
    }

}
