using D47.Core.Audio;
using D47.Core.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The rewrites a <see cref="SpeechPipeline"/> makes before the provider write English, so a voice carrying another
/// language is handed the line as written (#1752).
/// </summary>
public class ALineInAnotherLanguageIsNotRewrittenTests
{
    private static readonly VoiceSelection Japanese = new("ja-JP-NanamiNeural") { Language = SpokenLanguage.Japanese };

    private sealed class FakeClock : IWallClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 26, 14, 47, 0, TimeSpan.Zero);
    }

    private static async Task<string> SentAsync(string line, VoiceSelection voice, SpokenAddress? address = null)
    {
        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var tts = new FakeTtsProvider();

        await using (var pipeline = new SpeechPipeline(arbiter, tts, voice, "turn-1", NullLogger.Instance, address: address))
        {
            pipeline.Push(line);
            await pipeline.CompleteAsync();
        }

        return Assert.Single(tts.Requested);
    }

    [Fact]
    public async Task AUnitAbbreviationIsLeftAsWritten()
    {
        Assert.Equal("12 light years", await SentAsync("12 ly", VoiceSelection.Default));
        Assert.Equal("あと 12 ly", await SentAsync("あと 12 ly", Japanese));
    }

    [Fact]
    public async Task ADesignationNumberIsLeftAsWritten()
    {
        Assert.DoesNotContain("285", await SentAsync("Col 285 Sector", VoiceSelection.Default), StringComparison.Ordinal);
        Assert.Equal("Col 285 Sector へ", await SentAsync("Col 285 Sector へ", Japanese));
    }

    [Fact]
    public async Task ARepeatedAddressIsLeftAsWritten()
    {
        var address = new SpokenAddress(new FakeClock()) { CommanderName = "Doug DEPARAGON" };

        // The first address is kept, which puts the next one inside the window that drops it.
        await SentAsync("Welcome home, Commander DEPARAGON.", VoiceSelection.Default, address);

        Assert.Equal("ようこそ, Commander.", await SentAsync("ようこそ, Commander.", Japanese, address));
        Assert.Equal("Welcome home.", await SentAsync("Welcome home, Commander.", VoiceSelection.Default, address));
    }
}
