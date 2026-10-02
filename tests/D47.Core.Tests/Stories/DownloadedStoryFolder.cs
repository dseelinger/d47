using System.IO.Compression;
using System.Text;
using System.Text.Json;
using D47.Core.Stories;

namespace D47.Core.Tests.Stories;

/// <summary>A temporary <c>data\stories</c> folder, written the way the release publishes it.</summary>
internal sealed class DownloadedStoryFolder : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public DownloadedStoryFolder() => Directory.CreateDirectory(Path);

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d47-stories-" + Guid.NewGuid().ToString("N"));

    public void WriteIndex(params StoryCard[] cards) =>
        File.WriteAllText(System.IO.Path.Combine(Path, StoryCatalog.IndexFile), JsonSerializer.Serialize(cards, Json));

    public void WriteSealed(StorySecret secret)
    {
        var raw = JsonSerializer.SerializeToUtf8Bytes(new[] { secret }, Json);
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        File.WriteAllText(System.IO.Path.Combine(Path, secret.Id + StoryCatalog.SealedExtension), Convert.ToBase64String(packed.ToArray()), Encoding.ASCII);
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
