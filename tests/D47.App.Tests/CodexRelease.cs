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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.RequestUri!.AbsolutePath.EndsWith(CodexPictureIndex.IndexName, StringComparison.Ordinal))
        {
            await Hold.Task.ConfigureAwait(false);
        }

        return _files.TryGetValue(request.RequestUri.AbsolutePath, out var bytes)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
