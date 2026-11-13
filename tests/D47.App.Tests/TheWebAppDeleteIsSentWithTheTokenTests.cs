using System.Net;
using System.Net.Http;
using D47.App.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheWebAppDeleteIsSentWithTheTokenTests
{
    private sealed class Answers(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("a body that is never read") });
        }
    }

    private static async Task<(bool Deleted, HttpRequestMessage? Request)> Delete(HttpStatusCode status)
    {
        var handler = new Answers(status);
        using var client = new WebAppClient(_ => { }, NullLogger<WebAppClient>.Instance, handler, "https://web.example.test");

        return (await client.DeleteSnapshotAsync("upload-token", CancellationToken.None), handler.Request);
    }

    [Fact]
    public async Task ItIsADeleteOfTheSnapshotWithTheTokenAsBearer()
    {
        var (deleted, request) = await Delete(HttpStatusCode.NoContent);

        Assert.True(deleted);
        Assert.Equal(HttpMethod.Delete, request!.Method);
        Assert.Equal(new Uri("https://web.example.test/snapshot"), request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("upload-token", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ATokenTheWebAppNoLongerKnowsCountsAsDeleted() =>
        Assert.True((await Delete(HttpStatusCode.Unauthorized)).Deleted);

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AnyOtherRefusalIsNotDeleted(HttpStatusCode status) =>
        Assert.False((await Delete(status)).Deleted);
}
