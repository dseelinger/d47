using System.IO.Compression;
using System.Text.Json;
using D47.Core.Stories;
using D47.Core.Storage;

namespace D47.Core.Tests.Stories;

/// <summary>A <c>data\stories</c> folder in memory, written the way the release publishes it.</summary>
internal sealed class DownloadedStoryFolder
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public MemoryFileSystem Files { get; } = new();

    public string Path { get; } = @"C:\d47-test\data\stories";

    public void WriteIndex(params StoryCard[] cards) =>
        Files.WriteText(System.IO.Path.Combine(Path, StoryCatalog.IndexFile), JsonSerializer.Serialize(cards, Json));

    public void WriteSealed(StorySecret secret)
    {
        var raw = JsonSerializer.SerializeToUtf8Bytes(new[] { secret }, Json);
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        Files.WriteText(System.IO.Path.Combine(Path, secret.Id + StoryCatalog.SealedExtension), Convert.ToBase64String(packed.ToArray()));
    }
}
