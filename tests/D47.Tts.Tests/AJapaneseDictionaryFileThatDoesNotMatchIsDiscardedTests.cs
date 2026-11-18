using System.Net;
using D47.Core.Speech;
using Xunit;

namespace D47.Tts.Tests;

[Trait("Category", "Integration")]
public sealed class AJapaneseDictionaryFileThatDoesNotMatchIsDiscardedTests : IDisposable
{
    private sealed class Serves(byte[] body) : HttpMessageHandler
    {
        public List<Uri> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
    }

    private static readonly KokoroAsset Dicrc = JapaneseDictionaryAssets.All.Single(asset => asset.Path == "dicrc");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-tests", Guid.NewGuid().ToString("N"));

    private string Destination => JapaneseDictionaryAssets.Destination(_folder, Dicrc);

    [Fact]
    public async Task AFileWithTheWrongHashIsRefusedAndNothingIsLeft()
    {
        using var handler = new Serves(new byte[Dicrc.Bytes]);

        var result = await Fetch(handler);

        Assert.Equal(KokoroInstall.ChecksumMismatch, result.Outcome);
        Assert.False(File.Exists(Destination));
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task TheReleaseFileIsAskedForAndKept()
    {
        var shipped = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "dic", "dicrc"));
        using var handler = new Serves(shipped);

        var result = await Fetch(handler);

        Assert.Equal(KokoroInstall.Installed, result.Outcome);
        Assert.Equal(shipped, File.ReadAllBytes(Destination));
        Assert.Equal([new Uri(Dicrc.Url)], handler.Asked);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private async Task<KokoroInstallResult> Fetch(Serves handler)
    {
        using var http = new HttpClient(handler, disposeHandler: false);

        return await ModelDownload.FetchAsync(
            http, new RecordingLogger<AJapaneseDictionaryFileThatDoesNotMatchIsDiscardedTests>(),
            Dicrc, Destination, 0, Dicrc.Bytes, null, TestContext.Current.CancellationToken);
    }
}
