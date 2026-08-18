using System.Net;
using System.Net.Sockets;
using System.Text;

namespace D47.Llm.Tests;

/// <summary>
/// A loopback HTTP server that replays one recorded response, so the streaming decoder can be
/// driven end to end without contacting Anthropic.
/// <para>
/// <b>No test in this project makes a live API call.</b> What is under test is d47's half of the
/// exchange — assembling a tool call out of JSON fragments, merging usage across two events that
/// carry it differently, translating a stop reason, and turning a transport failure into a
/// <c>Failed</c> event rather than an exception crossing the seam. All of that is decoding, and
/// decoding needs a recording rather than a service.
/// </para>
/// <para>
/// A bare <see cref="TcpListener"/> rather than <c>HttpListener</c>, deliberately: HttpListener
/// wants a URL reservation for any prefix but <c>localhost:80</c> and answers "access denied"
/// without one, which would make this project fail on a developer machine and pass on an elevated
/// CI agent — or the other way round. Thirty lines of HTTP/1.1 has no such opinion.
/// </para>
/// <para>
/// It also means the request the SDK sent is capturable, which is the only way to assert on what
/// actually went out as opposed to what <c>BuildParameters</c> returned.
/// </para>
/// </summary>
internal sealed class RecordedEndpoint : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _serving;
    private readonly List<string> _requests = [];
    private readonly Lock _taken = new();

    /// <summary>
    /// The responses to serve, in order. The last one repeats once the list runs out, which is
    /// what makes a single-response fixture behave exactly as it did before there were several.
    /// </summary>
    private readonly IReadOnlyList<(int Status, string ContentType, string Body)> _responses;

    private int _served;

    /// <summary>Written and flushed before <see cref="_release"/> is awaited, or null.</summary>
    private readonly string? _opening;

    private readonly Task? _release;

    private RecordedEndpoint(params (int Status, string ContentType, string Body)[] responses)
        : this(null, responses, null)
    {
    }

    private RecordedEndpoint(
        Task? release,
        (int Status, string ContentType, string Body) response,
        string? opening)
        : this(release, [response], opening)
    {
    }

    private RecordedEndpoint(
        Task? release,
        (int Status, string ContentType, string Body)[] responses,
        string? opening)
    {
        _release = release;
        _opening = opening;
        _responses = responses;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();

        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        _serving = Task.Run(() => ServeAsync(_stopping.Token));
    }

    public string BaseUrl { get; }

    /// <summary>The bodies the client actually sent, in order.</summary>
    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_taken)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Replays a server-sent-event stream, which is what a streaming turn looks like.</summary>
    public static RecordedEndpoint Streaming(params string[] events) =>
        new((200, "text/event-stream", string.Concat(events)));

    /// <summary>Replays an error, so the failure translation can be driven by status code.</summary>
    public static RecordedEndpoint Failing(int status, string body) =>
        new((status, "application/json", body));

    /// <summary>
    /// Replays a stream that <em>stops part-way</em> and does not finish until the test says so.
    /// <para>
    /// The only fixture that can tell streaming from buffering. Every other recording arrives in
    /// one write, so a decoder that holds the whole turn back produces exactly the same sequence
    /// as one that passes it through — the difference is only ever <em>when</em>. Here the rest of
    /// the body is withheld until the caller has seen the first part, so a decoder that waits for
    /// the whole body sees nothing and fails on its own budget rather than hanging.
    /// </para>
    /// </summary>
    /// <param name="release">Completed by the test once it has seen what the first part carried.</param>
    public static RecordedEndpoint StreamingUntilReleased(Task release, string opening, params string[] rest) =>
        new(release, (200, "text/event-stream", string.Concat(rest)), opening);

    /// <summary>Replays one plain JSON body — a model list, or anything else that is not streamed.</summary>
    public static RecordedEndpoint Json(string body) =>
        new((200, "application/json", body));

    /// <summary>
    /// Replays a refusal and then a stream, which is the shape a demotion needs: the endpoint
    /// names a field it will not accept, and the retry without that field succeeds.
    /// <para>
    /// Two responses rather than one is the whole point — a fixture that answers identically
    /// every time cannot tell "retried once" from "sent once", which is exactly the property
    /// being asserted.
    /// </para>
    /// </summary>
    public static RecordedEndpoint RefusingThenStreaming(int status, string refusal, params string[] events) =>
        new((status, "application/json", refusal), (200, "text/event-stream", string.Concat(events)));

    /// <summary>
    /// One recorded SSE frame. Named and formatted here so the recordings below read as the wire
    /// format rather than as string concatenation.
    /// </summary>
    public static string Event(string name, string json) => $"event: {name}\ndata: {json}\n\n";

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            // Taken before the response is served, so concurrent connections cannot both be
            // handed the first entry. Clamped rather than wrapped: the last response repeats.
            var which = Math.Min(Interlocked.Increment(ref _served) - 1, _responses.Count - 1);
            var (status, contentType, body) = _responses[which];

            _ = Task.Run(
                async () =>
                {
                    using (client)
                    {
                        try
                        {
                            await RespondAsync(client, status, contentType, body, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // The client hanging up mid-response is an ordinary end to a
                            // cancelled turn, and this is a test fixture rather than a server.
                        }
                    }
                },
                CancellationToken.None);
        }
    }

    private async Task RespondAsync(
        TcpClient client,
        int status,
        string contentType,
        string body,
        CancellationToken cancellationToken)
    {
        var stream = client.GetStream();
        var buffer = new byte[8192];
        var received = new MemoryStream();
        var headerEnd = -1;

        // Headers first, then exactly as many body bytes as Content-Length promised. Reading
        // until the socket closes would deadlock: the SDK keeps the connection open for the
        // response it is waiting for.
        while (headerEnd < 0)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return;
            }

            received.Write(buffer, 0, read);
            headerEnd = IndexOfHeaderEnd(received.ToArray());
        }

        var all = received.ToArray();
        var headers = Encoding.UTF8.GetString(all, 0, headerEnd);
        var have = all.Length - (headerEnd + 4);

        var length = headers
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            ?.Split(':')[1]
            .Trim();

        var wanted = int.TryParse(length, out var declared) ? declared : 0;

        while (have < wanted)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            received.Write(buffer, 0, read);
            have += read;
        }

        all = received.ToArray();

        lock (_taken)
        {
            _requests.Add(Encoding.UTF8.GetString(all, headerEnd + 4, Math.Max(0, all.Length - (headerEnd + 4))));
        }

        var opening = Encoding.UTF8.GetBytes(_opening ?? string.Empty);
        var payload = Encoding.UTF8.GetBytes(body);

        // Chunked when the body is withheld, because Content-Length would have to state a total
        // the server has not decided to send yet — and a client that knows the length is entitled
        // to wait for all of it, which is the behaviour under test.
        var response = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status} {Reason(status)}\r\n"
            + $"Content-Type: {contentType}\r\n"
            + (_release is null
                ? $"Content-Length: {opening.Length + payload.Length}\r\n"
                : "Transfer-Encoding: chunked\r\n")
            + "Connection: close\r\n"
            + "\r\n");

        await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);

        if (_release is null)
        {
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteChunkAsync(stream, opening, cancellationToken).ConfigureAwait(false);
            await _release.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            await WriteChunkAsync(stream, payload, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("0\r\n\r\n"), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        client.Client.Shutdown(SocketShutdown.Send);
    }

    private static async Task WriteChunkAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
    {
        if (payload.Length == 0)
        {
            return;
        }

        await stream.WriteAsync(Encoding.UTF8.GetBytes($"{payload.Length:X}\r\n"), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(Encoding.UTF8.GetBytes("\r\n"), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int IndexOfHeaderEnd(byte[] bytes)
    {
        for (var i = 0; i + 3 < bytes.Length; i++)
        {
            if (bytes[i] == '\r' && bytes[i + 1] == '\n' && bytes[i + 2] == '\r' && bytes[i + 3] == '\n')
            {
                return i;
            }
        }

        return -1;
    }

    private static string Reason(int status) => status switch
    {
        200 => "OK",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        529 => "Overloaded",
        _ => "Status",
    };

    public void Dispose()
    {
        _stopping.Cancel();
        _listener.Stop();

        try
        {
            _serving.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Cancellation on the way out.
        }

        _stopping.Dispose();
    }
}
