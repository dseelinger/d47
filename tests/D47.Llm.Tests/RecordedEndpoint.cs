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

    private RecordedEndpoint(int status, string contentType, string body)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();

        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        _serving = Task.Run(() => ServeAsync(status, contentType, body, _stopping.Token));
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
        new(200, "text/event-stream", string.Concat(events));

    /// <summary>Replays an error, so the failure translation can be driven by status code.</summary>
    public static RecordedEndpoint Failing(int status, string body) =>
        new(status, "application/json", body);

    /// <summary>
    /// One recorded SSE frame. Named and formatted here so the recordings below read as the wire
    /// format rather than as string concatenation.
    /// </summary>
    public static string Event(string name, string json) => $"event: {name}\ndata: {json}\n\n";

    private async Task ServeAsync(int status, string contentType, string body, CancellationToken cancellationToken)
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

        var payload = Encoding.UTF8.GetBytes(body);

        var response = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status} {Reason(status)}\r\n"
            + $"Content-Type: {contentType}\r\n"
            + $"Content-Length: {payload.Length}\r\n"
            + "Connection: close\r\n"
            + "\r\n");

        await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        client.Client.Shutdown(SocketShutdown.Send);
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
