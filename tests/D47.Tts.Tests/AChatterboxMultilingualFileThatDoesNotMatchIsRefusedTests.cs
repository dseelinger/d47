using System.Net;
using D47.Core.Speech;
using D47.Core.Storage;
using Xunit;

namespace D47.Tts.Tests;

[Trait("Category", "Integration")]
public sealed class AChatterboxMultilingualFileThatDoesNotMatchIsRefusedTests : IDisposable
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

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AFileOfTheRightSizeButTheWrongHashIsDiscardedAndTheInstallStops()
    {
        using var handler = new Serves(new byte[ChatterboxMultilingualAssets.Tokenizer.Bytes]);
        using var http = new HttpClient(handler, disposeHandler: false);
        using var installer = new ChatterboxMultilingualInstaller(
            new DiskFileSystem(), _folder, new RecordingLogger<ChatterboxMultilingualInstaller>(), http);

        var result = await installer.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(KokoroInstall.ChecksumMismatch, result.Outcome);
        Assert.Equal([new Uri("https://huggingface.co/onnx-community/chatterbox-multilingual-ONNX/resolve/main/tokenizer.json")], handler.Asked);
        Assert.Empty(Directory.GetFiles(_folder, "*", SearchOption.AllDirectories));
        Assert.False(installer.IsInstalled);
    }

    [Fact]
    public void ItsFilesAreNineAndApartFromTurbos()
    {
        Assert.Equal(9, ChatterboxMultilingualAssets.All.Count);
        Assert.Equal(1_555_105_907, ChatterboxMultilingualAssets.All.Sum(asset => asset.Bytes));
        Assert.Equal("1,483", ChatterboxMultilingualAssets.TotalMegabytesText);
        Assert.Empty(ChatterboxMultilingualAssets.All.Select(asset => asset.Sha256).Intersect(ChatterboxAssets.All.Select(asset => asset.Sha256)));
        Assert.Equal(
            Path.Combine(_folder, "onnx", "language_model_q4.onnx"),
            ChatterboxMultilingualAssets.GraphPath(_folder, "language_model"));
        Assert.Equal(
            Path.Combine(_folder, "onnx", "conditional_decoder.onnx"),
            ChatterboxMultilingualAssets.GraphPath(_folder, "conditional_decoder"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
