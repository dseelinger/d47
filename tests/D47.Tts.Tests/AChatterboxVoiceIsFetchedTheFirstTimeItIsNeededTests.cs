using System.Security.Cryptography;
using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Tts.Tests;

public sealed class AChatterboxVoiceIsFetchedTheFirstTimeItIsNeededTests
{
    /// <summary>Records the first sample of each reference clip it encodes, which tells the clips apart.</summary>
    private sealed class Engine : IChatterboxEngine
    {
        public List<float> Encoded { get; } = [];

        public IDisposable Encode(float[] reference)
        {
            Encoded.Add(MathF.Round(reference[0], 2));
            return new Handle();
        }

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

    private sealed class Recorded : ILogger<ChatterboxTtsProvider>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Lines)
            {
                Lines.Add(formatter(state, exception));
            }
        }
    }

    /// <summary>The shipped clip in the test folder is silence; this one starts at a quarter.</summary>
    private const float WrenSample = 0.25f;

    private static readonly byte[] Wren =
        WavWriter.ToBytes([.. Enumerable.Repeat(WrenSample, ChatterboxVoices.SampleRate * 6)], ChatterboxVoices.SampleRate);

    private readonly ChatterboxTestFolder _folder = new();
    private readonly Engine _engine = new();
    private readonly Recorded _log = new();
    private readonly List<Uri> _asked = [];

    private Func<Uri, long, CancellationToken, Task<byte[]>> _download;

    public AChatterboxVoiceIsFetchedTheFirstTimeItIsNeededTests()
    {
        _download = (url, _, _) => Task.FromResult(Wren);

        _folder.Files.WriteText(
            Path.Combine(_folder.Voices, ChatterboxCatalog.TableName),
            "id\tname\tgender\tlocale\tpitch\tpace\trole\tsource\tsha256\tbytes\n"
            + $"marlow\tMarlow\tfemale\ten\tmid\teven\tShipAi\ta test clip\t{new string('0', 64)}\t1\n"
            + $"wren\tWren\tfemale\ten\thigh\tbrisk\t\ta test clip\t{Convert.ToHexStringLower(SHA256.HashData(Wren))}\t{Wren.Length}\n");
    }

    private string WrenClip => Path.Combine(_folder.Fetched, "wren.wav");

    private ChatterboxTtsProvider Provider() =>
        new(
            _folder.Files,
            _folder.Models,
            _folder.Voices,
            _folder.Fetched,
            _log,
            () => _engine,
            () => true,
            (url, bytes, token) =>
            {
                lock (_asked)
                {
                    _asked.Add(url);
                }

                return _download(url, bytes, token);
            })
        {
            FirstLineWait = TimeSpan.FromSeconds(10),
        };

    private static Task<AudioClip> Say(ChatterboxTtsProvider provider, string voice) =>
        provider.SynthesizeAsync("hi", new VoiceSelection(voice) { Role = VoiceRole.Comms }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task EveryCatalogueRowIsListedWithItsBandsWhetherOrNotItIsHere()
    {
        using var provider = Provider();

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["marlow", "wren"], listed.Voices.Select(voice => voice.Id));
        Assert.Equal("high pitch, brisk pace", listed.Voices[1].Description);
        Assert.Equal("female", listed.Voices[1].Gender);
        Assert.Equal(("high", "brisk"), (provider.Catalogue()[1].Pitch, provider.Catalogue()[1].Pace));
        Assert.Empty(_asked);
    }

    [Fact]
    public async Task AShippedVoiceIsSpokenFromBesideTheExeWithNoFetch()
    {
        using var provider = Provider();

        await Say(provider, "marlow");

        Assert.Empty(_asked);
        Assert.Equal([0f], _engine.Encoded);
        Assert.Empty(_folder.Files.Enumerate(_folder.Fetched, "*"));
    }

    [Fact]
    public async Task AMatchingClipInTheDataFolderIsUsedWithNoFetch()
    {
        _folder.Files.WriteBytes(WrenClip, Wren);
        using var provider = Provider();

        await Say(provider, "wren");

        Assert.Empty(_asked);
        Assert.Equal([WrenSample], _engine.Encoded);
    }

    [Fact]
    public async Task AMismatchedClipIsDeletedAndFetchedAgain()
    {
        var tampered = (byte[])Wren.Clone();
        tampered[^1] ^= 0x7f;
        _folder.Files.WriteBytes(WrenClip, tampered);
        using var provider = Provider();

        await Say(provider, "wren");

        Assert.Equal(
            new Uri("https://github.com/dseelinger/d47/releases/download/chatterbox-voices-1/wren.wav"),
            Assert.Single(_asked));
        Assert.Equal(Wren, _folder.Files.ReadBytes(WrenClip));
    }

    [Fact]
    public async Task AFileWithNoRowIsNeverLoaded()
    {
        _folder.Files.WriteBytes(Path.Combine(_folder.Fetched, "stranger.wav"), Wren);
        using var provider = Provider();

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(listed.Voices, voice => voice.Id == "stranger");
        await Assert.ThrowsAsync<TtsException>(() => Say(provider, "stranger"));
        Assert.Empty(_engine.Encoded);
        Assert.Empty(_asked);
    }

    [Fact]
    public async Task AVoiceNotHereIsFetchedOnceAndThenSpokenInItself()
    {
        using var provider = Provider();

        await Say(provider, "wren");
        await Say(provider, "wren");

        Assert.Single(_asked);
        Assert.Equal(Wren, _folder.Files.ReadBytes(WrenClip));
        Assert.Equal([WrenSample], _engine.Encoded);
    }

    [Fact]
    public async Task AVoiceWhoseClipCannotArriveIsSpokenInAShippedVoiceAndTheLogNamesBoth()
    {
        _download = (_, _, _) => throw new HttpRequestException("the network is down");
        using var provider = Provider();

        var listed = await provider.ListVoicesAsync(TestContext.Current.CancellationToken);
        await Say(provider, "wren");

        Assert.Contains(listed.Voices, voice => voice.Id == "wren");
        Assert.Equal([0f], _engine.Encoded);
        Assert.Contains(_log.Lines, line => line.Contains("wren", StringComparison.Ordinal) && line.Contains("marlow", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailedFetchIsLoggedOnceAndTriedAgainOnlyWhenTheVoiceIsPickedOrPreviewed()
    {
        _download = (_, _, _) => throw new HttpRequestException("the network is down");
        using var provider = Provider();

        await Say(provider, "wren");
        await Say(provider, "wren");

        Assert.Single(_asked);

        Assert.False(await provider.FetchAsync("wren", TestContext.Current.CancellationToken));

        Assert.Equal(2, _asked.Count);
        Assert.Single(_log.Lines, line => line.Contains("could not be fetched", StringComparison.Ordinal));

        _download = (_, _, _) => Task.FromResult(Wren);

        Assert.True(await provider.FetchAsync("wren", TestContext.Current.CancellationToken));
        await Say(provider, "wren");

        Assert.Equal(WrenSample, _engine.Encoded[^1]);
    }

    [Fact]
    public async Task AClipThatArrivesWrongIsNotKept()
    {
        _download = (_, _, _) => Task.FromResult(Wren[..^1]);
        using var provider = Provider();

        Assert.False(await provider.FetchAsync("wren", TestContext.Current.CancellationToken));

        Assert.Null(_folder.Files.Stat(WrenClip));
        Assert.Empty(_folder.Files.Enumerate(_folder.Fetched, "*"));
    }

    [Fact]
    public async Task AnIdNotInTheCatalogueIsNeverFetched()
    {
        using var provider = Provider();

        Assert.False(await provider.FetchAsync(@"..\..\evil", TestContext.Current.CancellationToken));
        Assert.True(await provider.FetchAsync("marlow", TestContext.Current.CancellationToken));

        Assert.Empty(_asked);
    }
}
