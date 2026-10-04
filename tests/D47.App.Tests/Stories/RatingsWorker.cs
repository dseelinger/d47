using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using D47.App.Panel;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests.Stories;

/// <summary>A stand-in for the ratings Worker: records each request, serves a fixed list, and answers votes with a fixed aggregate.</summary>
internal sealed class RatingsWorker : HttpMessageHandler
{
    private readonly ConcurrentQueue<Asked> _asked = new();

    public sealed record Asked(string Method, string Path, string? Format, string? Voter, string? Body);

    public string List { get; set; } = """{"format":1,"stories":{}}""";

    public string Reply { get; set; } = """{"average":4.5,"count":2}""";

    /// <summary>When set, every request fails as though the network were down.</summary>
    public bool Down { get; set; }

    /// <summary>When set, every vote and withdrawal is answered 503 while the list is still served.</summary>
    public bool RefuseVotes { get; set; }

    public IReadOnlyList<Asked> Requests => [.. _asked];

    public StoryRatingClient Client(StoryStore stories, bool allowed = true) =>
        new(stories, () => allowed, NullLogger.Instance, this, "https://ratings.test");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _asked.Enqueue(new Asked(
            request.Method.Method,
            request.RequestUri!.AbsolutePath,
            request.Headers.TryGetValues("d47-format", out var format) ? format.Single() : null,
            request.Headers.TryGetValues("d47-voter", out var voter) ? voter.Single() : null,
            body));

        if (Down)
        {
            throw new HttpRequestException("The network is down.");
        }

        if (RefuseVotes && request.Method != HttpMethod.Get)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.Method == HttpMethod.Get ? List : Reply, Encoding.UTF8, "application/json"),
        };
    }
}
