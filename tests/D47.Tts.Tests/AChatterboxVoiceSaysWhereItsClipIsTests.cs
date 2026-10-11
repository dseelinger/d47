using System.Net;
using System.Security.Cryptography;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

public sealed class AChatterboxVoiceSaysWhereItsClipIsTests
{
    private sealed class Engine : IChatterboxEngine
    {
        public IDisposable Encode(float[] reference) => new Handle();

        public float[] Speak(long[] textIds, IDisposable voice, CancellationToken cancellationToken) =>
            new float[ChatterboxPipeline.SampleRate / 10];

        public void Dispose()
        {
        }

        private sealed class Handle : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    /// <summary>Answers every request with one status and no body.</summary>
    private sealed class Answering(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private static readonly byte[] Wren =
        WavWriter.ToBytes([.. Enumerable.Repeat(0.25f, ChatterboxVoices.SampleRate * 6)], ChatterboxVoices.SampleRate);

    private readonly ChatterboxTestFolder _folder = new();
    private readonly CustomVoices _custom;

    private Func<Uri, long, CancellationToken, Task<byte[]>> _download = ChatterboxTestFolder.NoDownload;

    public AChatterboxVoiceSaysWhereItsClipIsTests()
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(Wren));

        _folder.Files.WriteText(
            Path.Combine(_folder.Voices, ChatterboxCatalog.TableName),
            "id\tname\tgender\tlocale\tpitch\tpace\trole\tsource\tsha256\tbytes\n"
            + $"marlow\tMarlow\tfemale\ten\tmid\teven\tShipAi\ta test clip\t{new string('0', 64)}\t1234\n"
            + $"wren\tWren\tfemale\ten\thigh\tbrisk\t\ta test clip\t{sha}\t{Wren.Length}\n"
            + $"rook\tRook\tmale\ten\tlow\tslow\t\ta test clip\t{sha}\t{Wren.Length}\n");

        _custom = new CustomVoices(Path.Combine(_folder.Root, "data"), _folder.Files, new DpapiSecretProtector());
    }

    private ChatterboxTtsProvider Provider() =>
        new(
            _folder.Files,
            _folder.Models,
            _folder.Voices,
            _folder.Fetched,
            NullLogger<ChatterboxTtsProvider>.Instance,
            () => new Engine(),
            () => true,
            (url, bytes, token) => _download(url, bytes, token),
            custom: _custom);

    private static Func<Uri, long, CancellationToken, Task<byte[]>> Held(Dictionary<string, TaskCompletionSource<byte[]>> held) =>
        (url, _, _) => held[Path.GetFileNameWithoutExtension(url.AbsolutePath)].Task;

    private static Func<Uri, long, CancellationToken, Task<byte[]>> AnsweringWith(HttpStatusCode status) =>
        (url, bytes, token) => ChatterboxClipDownload.GetAsync(new HttpClient(new Answering(status)), url, bytes, token);

    [Fact]
    public void AShippedVoiceIsShippedWithItsCatalogueSize()
    {
        using var provider = Provider();

        Assert.Equal(new ChatterboxClipState(ChatterboxClip.Shipped, 1234), provider.ClipState("marlow"));
    }

    [Fact]
    public void AMatchingClipOnThisPcIsFetched()
    {
        _folder.Files.WriteBytes(Path.Combine(_folder.Fetched, "wren.wav"), Wren);
        using var provider = Provider();

        Assert.Equal(new ChatterboxClipState(ChatterboxClip.Fetched, Wren.Length), provider.ClipState("wren"));
    }

    [Fact]
    public void NoClipOnThisPcIsNotFetched()
    {
        using var provider = Provider();

        Assert.Equal(new ChatterboxClipState(ChatterboxClip.NotFetched, Wren.Length), provider.ClipState("wren"));
    }

    [Fact]
    public async Task AFetchHeldOpenIsFetchingUntilTheClipArrives()
    {
        var held = new Dictionary<string, TaskCompletionSource<byte[]>> { ["wren"] = new() };
        _download = Held(held);
        using var provider = Provider();

        var fetch = provider.FetchAsync("wren", TestContext.Current.CancellationToken);

        Assert.Equal(new ChatterboxClipState(ChatterboxClip.Fetching, Wren.Length), provider.ClipState("wren"));

        held["wren"].SetResult(Wren);

        Assert.True(await fetch);
        Assert.Equal(ChatterboxClip.Fetched, provider.ClipState("wren")?.Clip);
    }

    [Fact]
    public async Task AFetchThatAnswers503IsFailedWithTheReleasesReasonUntilAFetchSucceeds()
    {
        _download = AnsweringWith(HttpStatusCode.ServiceUnavailable);
        using var provider = Provider();

        Assert.False(await provider.FetchAsync("wren", TestContext.Current.CancellationToken));

        Assert.Equal(
            new ChatterboxClipState(ChatterboxClip.Failed, Wren.Length) { Reason = "the release answered 503 Service Unavailable" },
            provider.ClipState("wren"));

        _download = (_, _, _) => Task.FromResult(Wren);

        Assert.True(await provider.FetchAsync("wren", TestContext.Current.CancellationToken));
        Assert.Equal(new ChatterboxClipState(ChatterboxClip.Fetched, Wren.Length), provider.ClipState("wren"));
    }

    [Fact]
    public void ACustomVoiceIsCustomAndAnUnlistedOneHasNoState()
    {
        Assert.Null(_custom.Save(
            "Secret Ally",
            "female",
            null,
            null,
            [.. Enumerable.Repeat(0.5f, ChatterboxVoices.SampleRate * 6)],
            ChatterboxVoices.SampleRate,
            out var id));
        using var provider = Provider();

        Assert.Equal(new ChatterboxClipState(ChatterboxClip.Custom, 0), provider.ClipState(id!));
        Assert.Null(provider.ClipState("stranger"));
        Assert.Null(provider.ClipState(OwnVoice.VoiceId));
    }

    [Fact]
    public async Task TheNoticeIsRaisedWhenAFetchStartsSucceedsAndFails()
    {
        var held = new Dictionary<string, TaskCompletionSource<byte[]>> { ["wren"] = new(), ["rook"] = new() };
        _download = Held(held);
        using var provider = Provider();
        var seen = new List<(string, ChatterboxClip?)>();

        provider.ClipStateChanged += id =>
        {
            lock (seen)
            {
                seen.Add((id, provider.ClipState(id)?.Clip));
            }
        };

        var wren = provider.FetchAsync("wren", TestContext.Current.CancellationToken);
        held["wren"].SetResult(Wren);
        await wren;

        var rook = provider.FetchAsync("rook", TestContext.Current.CancellationToken);
        held["rook"].SetException(new HttpRequestException("the release answered 503 Service Unavailable"));
        await rook;

        Assert.Equal(
            [
                ("wren", ChatterboxClip.Fetching),
                ("wren", ChatterboxClip.Fetched),
                ("rook", ChatterboxClip.Fetching),
                ("rook", ChatterboxClip.Failed),
            ],
            seen);
    }
}
