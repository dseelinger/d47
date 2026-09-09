using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheLogSaysWhichVoiceSpokeTests
{
    private static async Task<IReadOnlyList<string>> SpokenAsync(VoiceSelection voice, string? speaker)
    {
        var log = new List<string>();
        var arbiter = new AudioArbiter(
            new RecordingAudioSink(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AudioArbiter>.Instance).Start();

        await using (var speech = new SpeechPipeline(
            arbiter,
            new FakeTtsProvider(),
            voice,
            "test",
            new Capture(log),
            speaker: speaker))
        {
            speech.Push("Hull integrity is nominal.");
            await speech.CompleteAsync();
        }

        return log;
    }

    [Fact]
    public async Task ANamedVoiceIsNamedWithItsIdBesideIt()
    {
        var said = await SpokenAsync(
            new VoiceSelection("JBFqnCBsd6RMkjVDRZzb") { Name = "George" },
            speaker: "D47");

        var line = Assert.Single(said, message => message.StartsWith("Spoken by", StringComparison.Ordinal));

        // The role, the name and the id, which is what the Commander asked for.
        Assert.Contains("Spoken by D47", line, StringComparison.Ordinal);
        Assert.Contains("George", line, StringComparison.Ordinal);
        Assert.Contains("JBFqnCBsd6RMkjVDRZzb", line, StringComparison.Ordinal);
    }

    /// <summary>And which service said it (2026-08-28).</summary>
    [Fact]
    public async Task TheProviderIsNamedBesideTheVoice()
    {
        var said = await SpokenAsync(new VoiceSelection("pFQStpMdprGFILRDrWR2"), speaker: "Boe Dock");
        var line = Assert.Single(said, message => message.StartsWith("Spoken by", StringComparison.Ordinal));

        Assert.Contains(new FakeTtsProvider().Name, line, StringComparison.Ordinal);
    }

    /// <summary>
    /// And where nothing could resolve a name, the line is exactly what it always was rather than a gap
    /// or a repeated id.
    /// </summary>
    [Fact]
    public async Task AnUnnamedVoiceIsStillTheIdAndNothingElse()
    {
        var said = await SpokenAsync(new VoiceSelection("JBFqnCBsd6RMkjVDRZzb"), speaker: "D47");
        var line = Assert.Single(said, message => message.StartsWith("Spoken by", StringComparison.Ordinal));

        Assert.Contains("in JBFqnCBsd6RMkjVDRZzb", line, StringComparison.Ordinal);
        Assert.DoesNotContain("(JBFqnCBsd6RMkjVDRZzb)", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoVoiceAtAllSaysSoInWords()
    {
        var said = await SpokenAsync(VoiceSelection.Default, speaker: "Tower");
        var line = Assert.Single(said, message => message.StartsWith("Spoken by", StringComparison.Ordinal));

        Assert.Contains("Spoken by Tower", line, StringComparison.Ordinal);
        Assert.Contains("the provider's own voice", line, StringComparison.Ordinal);
    }

    private sealed class Capture(List<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }
}
