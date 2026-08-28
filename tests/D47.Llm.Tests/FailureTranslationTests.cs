using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// What a failed turn looks like coming out of the seam (Phase 2).
/// <para>
/// Two properties, and the second is the one with teeth. <b>Nothing leaves as an exception</b> —
/// the turn loop has no handler for one and a transport failure that escaped would take the tick
/// with it. And <b>transient and permanent are kept apart</b>: transient means the same request
/// may work shortly, and anything else needs a settings change before it is worth retrying. Get
/// that backwards and a bad key is retried forever, or an overload disables the model for the
/// session.
/// </para>
/// </summary>
public class FailureTranslationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string ErrorBody =
        """{"type":"error","error":{"type":"invalid_request_error","message":"recorded"}}""";

    private static async Task<LlmStreamEvent.Failed> FailureFrom(int status)
    {
        using var endpoint = RecordedEndpoint.Failing(status, ErrorBody);

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        return Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(events));
    }

    /// <summary>
    /// Rate limiting and a server error both clear on their own, so both are worth retrying.
    /// </summary>
    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(529)]
    public async Task AProblemAtTheirEndIsTransient(int status)
    {
        Assert.True((await FailureFrom(status)).Transient);
    }

    /// <summary>
    /// A rejected key is not. Retrying it forever produces nothing but bills, and the Commander
    /// is never told the one thing they can act on.
    /// </summary>
    [Fact]
    public async Task ARejectedKeyIsPermanent()
    {
        var failure = await FailureFrom(401);

        Assert.False(failure.Transient);
        Assert.Contains("key", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A key that exists but may not use the model, and a model name that does not exist, are
    /// both settings problems — and they are different settings, so they say different things.
    /// </summary>
    [Fact]
    public async Task ForbiddenAndNotFoundSayWhichSettingIsWrong()
    {
        var forbidden = await FailureFrom(403);
        var missing = await FailureFrom(404);

        Assert.False(forbidden.Transient);
        Assert.False(missing.Transient);

        Assert.Contains("not permitted", forbidden.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model", missing.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(forbidden.Message, missing.Message);
    }

    /// <summary>
    /// A failure ends the stream. Emitting one and then carrying on would leave the turn loop
    /// with both a failure and a completion for the same turn.
    /// </summary>
    [Fact]
    public async Task AFailureIsTheLastThingEmitted()
    {
        using var endpoint = RecordedEndpoint.Failing(500, ErrorBody);

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(events));
        Assert.DoesNotContain(events, step => step is LlmStreamEvent.Completed);
    }

    /// <summary>
    /// Nothing anywhere in the message is the key. The messages are the provider's own sentences
    /// for the cases it recognises, and an exception's <c>Message</c> otherwise — and the key is
    /// a header the exception never held.
    /// </summary>
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task TheKeyIsNeverInTheMessage(int status)
    {
        Assert.DoesNotContain("test-key", (await FailureFrom(status)).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An endpoint that is not there at all — the Commander pointed <c>llm.endpoint</c> at a
    /// gateway that is down. Transient, because it is the network rather than the configuration,
    /// and still not an exception.
    /// </summary>
    [Fact]
    public async Task AnUnreachableEndpointFailsRatherThanThrows()
    {
        // Bound and immediately released, so the port is almost certainly closed. Nothing here
        // leaves the machine: 127.0.0.1 with no listener refuses the connection locally.
        int port;

        using (var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
        }

        var provider = new AnthropicLlmProvider("test-key", $"http://127.0.0.1:{port}");
        var events = new List<LlmStreamEvent>();

        await foreach (var step in provider.StreamAsync(Recordings.Request(), Token))
        {
            events.Add(step);
        }

        var failure = Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(events));

        Assert.True(failure.Transient);
    }

    /// <summary>
    /// Cancellation is the Commander calling the turn off, and it is the one thing that <em>does</em>
    /// leave as an exception — the turn loop distinguishes "stopped" from "failed", and a
    /// cancelled turn reported as a failure would sound like d47 broke when it was told to stop.
    /// </summary>
    [Fact]
    public async Task CancellationIsNotAFailure()
    {
        using var endpoint = RecordedEndpoint.Streaming(Recordings.OneWord());
        using var cancelled = new CancellationTokenSource();

        await cancelled.CancelAsync();

        var provider = new AnthropicLlmProvider("test-key", endpoint.BaseUrl);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in provider.StreamAsync(Recordings.Request(), cancelled.Token))
            {
                // Draining is the point; the enumeration itself is what must throw.
            }
        });
    }
}
