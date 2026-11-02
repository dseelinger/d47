namespace D47.App.Tests;

internal static class TranscriberDoubles
{
    /// <summary>A handler whose every request throws <paramref name="exception"/>.</summary>
    public static HttpMessageHandler Throws(Exception exception) => new ThrowingHandler(exception);

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel) => throw exception;
    }
}
