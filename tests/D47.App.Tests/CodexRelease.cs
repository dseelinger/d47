using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using D47.Core.Interface;

namespace D47.App.Tests;

/// <summary>A codex picture release that serves the files it was given once <see cref="Hold"/> is set; the index is never held.</summary>
internal sealed class HeldRelease : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public void Serve(CodexPictureRow row, byte[] bytes) =>
        _files[$"/{CodexPictureIndex.Repository}/releases/download/{row.Tag}/{row.Asset}"] = bytes;

    public TaskCompletionSource Hold { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The path of every request, in the order it arrived.</summary>
    public ConcurrentQueue<string> Asked { get; } = new();

    /// <summary>Whether <paramref name="row"/>'s picture has been asked for.</summary>
    public bool WasAskedFor(CodexPictureRow row) => Asked.Any(path => path.EndsWith("/" + row.Asset, StringComparison.Ordinal));

    /// <summary>Waits until <paramref name="row"/>'s picture has been asked for.</summary>
    public void AwaitAsk(CodexPictureRow row)
    {
        var until = DateTime.UtcNow.AddSeconds(10);

        while (!WasAskedFor(row))
        {
            Xunit.Assert.True(DateTime.UtcNow < until, $"{row.Asset} was never asked for");
            Thread.Sleep(5);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Asked.Enqueue(request.RequestUri!.AbsolutePath);

        if (!request.RequestUri!.AbsolutePath.EndsWith(CodexPictureIndex.IndexName, StringComparison.Ordinal))
        {
            await Hold.Task.ConfigureAwait(false);
        }

        return _files.TryGetValue(request.RequestUri.AbsolutePath, out var bytes)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
