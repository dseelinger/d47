using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

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

    /// <summary>Rate limiting and a server error both clear on their own, so both are worth retrying.</summary>
    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(529)]
    public async Task AProblemAtTheirEndIsTransient(int status)
    {
        Assert.True((await FailureFrom(status)).Transient);
    }

    [Fact]
    public async Task ARejectedKeyIsPermanent()
    {
        var failure = await FailureFrom(401);

        Assert.False(failure.Transient);
        Assert.Contains("key", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

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

    [Fact]
    public async Task AFailureIsTheLastThingEmitted()
    {
        using var endpoint = RecordedEndpoint.Failing(500, ErrorBody);

        var events = await Recordings.DrainAsync(endpoint, cancellationToken: Token);

        Assert.IsType<LlmStreamEvent.Failed>(Assert.Single(events));
        Assert.DoesNotContain(events, step => step is LlmStreamEvent.Completed);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task TheKeyIsNeverInTheMessage(int status)
    {
        Assert.DoesNotContain("test-key", (await FailureFrom(status)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableEndpointFailsRatherThanThrows()
    {
        // Bound and immediately released, so the port is almost certainly closed.
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

    /// <summary>Cancellation is the one thing that leaves the seam as an exception.</summary>
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
