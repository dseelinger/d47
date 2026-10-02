using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using D47.App.Panel;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests.Stories;

/// <summary>A stand-in for the stories release: serves files by name, counts requests, and can hold a response.</summary>
internal sealed class StoryRelease : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _asked = new();

    public StoryRelease() => Directory.CreateDirectory(Folder);

    /// <summary>Where the downloader writes.</summary>
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "d47-story-release-" + Guid.NewGuid().ToString("N"));

    /// <summary>When set, a request for a file ending in this suffix waits for the task before it is answered.</summary>
    public (string Suffix, Task Until)? Hold { get; set; }

    public IReadOnlyCollection<string> Asked => [.. _asked];

    public void Serve(string file, byte[] bytes) => _files[file] = bytes;

    public void ServeIndex(params StoryCard[] cards) => Serve(StoryCatalog.IndexFile, JsonSerializer.SerializeToUtf8Bytes(cards, Json));

    public void ServeSealed(StorySecret secret)
    {
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(JsonSerializer.SerializeToUtf8Bytes(new[] { secret }, Json));
        }

        Serve(secret.Id + StoryCatalog.SealedExtension, Encoding.ASCII.GetBytes(Convert.ToBase64String(packed.ToArray())));
    }

    public StoryDownloader Downloader(bool allowed = true) =>
        new(Folder, () => allowed, NullLogger.Instance, this);

    public void Clean()
    {
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var file = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Split('/')[^1]);
        _asked.Enqueue(file);

        if (Hold is { } hold && file.EndsWith(hold.Suffix, StringComparison.Ordinal))
        {
            await hold.Until.ConfigureAwait(false);
        }

        return _files.TryGetValue(file, out var bytes)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Clean();
        }
    }
}
