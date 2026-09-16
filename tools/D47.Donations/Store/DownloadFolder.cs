using System.IO.Compression;
using System.Security.Cryptography;
using D47.Core.Storage;

namespace D47.Donations.Store;

/// <summary>
/// The one folder downloads go to, and the only thing that deletes from it.
/// </summary>
public sealed class DownloadFolder(string folder)
{
    /// <summary>
    /// What a decompressed payload may reach before this refuses it. The Worker caps a corpus at 96 MB
    /// compressed, and gzip on JSON lines runs about twelve to one.
    /// </summary>
    public const long MostDecompressedBytes = 2L * 1024 * 1024 * 1024;

    public string Folder { get; } = Path.GetFullPath(folder);

    /// <summary>
    /// Writes one zip holding the decompressed payload, and only when the payload hashes to what the
    /// object says it should. A mismatch writes nothing.
    /// </summary>
    public DownloadResult Save(DonationKey key, byte[] payload, string? expectedSha256)
    {
        if (string.IsNullOrEmpty(expectedSha256))
        {
            return new DownloadResult(key.ZipName, false, "the object carries no sha256 metadata");
        }

        var actual = Convert.ToHexStringLower(SHA256.HashData(payload));

        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            return new DownloadResult(
                key.ZipName,
                false,
                $"SHA-256 mismatch — the object says {expectedSha256}, the payload hashes to {actual}");
        }

        Directory.CreateDirectory(Folder);

        var path = Path.Combine(Folder, key.ZipName);
        var pending = path + AtomicFile.PendingSuffix;

        using (var file = File.Create(pending))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
        using (var entry = zip.CreateEntry(key.EntryName, CompressionLevel.Optimal).Open())
        {
            entry.Write(payload);
        }

        File.Move(pending, path, overwrite: true);

        return new DownloadResult(key.ZipName, true, null);
    }

    /// <summary>
    /// Deletes the local zip of every recorded download the store no longer holds, and names them. A
    /// zip that will not delete keeps its record, so the next listing tries again.
    /// </summary>
    public IReadOnlyList<string> Prune(UtilityState state, IReadOnlySet<string> keysStillInTheStore)
    {
        var deleted = new List<string>();

        foreach (var (key, zip) in state.Downloaded.ToArray())
        {
            if (keysStillInTheStore.Contains(key))
            {
                continue;
            }

            var path = Path.Combine(Folder, zip);

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted.Add(zip);
                }
            }
            catch (IOException)
            {
                continue;
            }

            state.Downloaded.Remove(key);
        }

        return deleted;
    }

    /// <summary>Unpacks a stored object exactly once.</summary>
    public static byte[] Decompress(byte[] compressed)
    {
        using var source = new MemoryStream(compressed);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var payload = new MemoryStream();

        var buffer = new byte[81920];
        int read;

        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (payload.Length + read > MostDecompressedBytes)
            {
                throw new InvalidDataException(
                    $"The payload passed {MostDecompressedBytes / (1024 * 1024)} MB decompressed and was not read further.");
            }

            payload.Write(buffer, 0, read);
        }

        return payload.ToArray();
    }
}

/// <summary>Whether a zip was written, and why not when it was not.</summary>
public sealed record DownloadResult(string ZipName, bool Written, string? Problem);
