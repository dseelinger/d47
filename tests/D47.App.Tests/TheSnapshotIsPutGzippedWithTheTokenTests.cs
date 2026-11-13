using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using D47.App.Web;
using D47.Core.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheSnapshotIsPutGzippedWithTheTokenTests
{
    private sealed class Answers(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public byte[]? Body { get; private set; }

        public long? Length { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Length = request.Content!.Headers.ContentLength;
            Body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("a body that is never read") };
        }
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no route");
    }

    private static byte[] Gzip(string text)
    {
        using var compressed = new MemoryStream();

        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(text));
        }

        return compressed.ToArray();
    }

    private static async Task<(WebAppPutResult Result, Answers Handler)> Put(HttpStatusCode status)
    {
        var handler = new Answers(status);
        using var client = new WebAppClient(_ => { }, NullLogger<WebAppClient>.Instance, handler, "https://web.example.test");

        return (await client.PutSnapshotAsync("upload-token", Gzip("{\"frontierId\":\"F1\"}"), CancellationToken.None), handler);
    }

    [Fact]
    public async Task ItIsAPutOfGzippedJsonWithItsLengthAndTheTokenAsBearer()
    {
        var (result, handler) = await Put(HttpStatusCode.NoContent);
        var request = handler.Request!;

        Assert.True(result.Stored);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal(new Uri("https://web.example.test/snapshot"), request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("upload-token", request.Headers.Authorization.Parameter);
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(["gzip"], request.Content.Headers.ContentEncoding);
        Assert.Equal(handler.Body!.Length, handler.Length);

        using var gzip = new GZipStream(new MemoryStream(handler.Body), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        Assert.Equal("{\"frontierId\":\"F1\"}", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A401IsReportedAsUnauthorized()
    {
        var (result, _) = await Put(HttpStatusCode.Unauthorized);

        Assert.False(result.Stored);
        Assert.True(result.Unauthorized);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    public async Task AnyOtherRefusalIsAFailureWithItsStatus(HttpStatusCode status)
    {
        var (result, _) = await Put(status);

        Assert.False(result.Stored);
        Assert.False(result.Unauthorized);
        Assert.Equal((int)status, result.Status);
    }

    [Fact]
    public async Task ARequestThatNeverArrivesIsAFailureNamedByItsError()
    {
        using var client = new WebAppClient(_ => { }, NullLogger<WebAppClient>.Instance, new Unreachable(), "https://web.example.test");

        var result = await client.PutSnapshotAsync("upload-token", Gzip("{}"), CancellationToken.None);

        Assert.False(result.Stored);
        Assert.Null(result.Status);
        Assert.Equal(nameof(HttpRequestException), result.Error);
    }
}
