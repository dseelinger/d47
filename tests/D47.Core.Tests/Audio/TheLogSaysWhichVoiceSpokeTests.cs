using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Who spoke, in what, and which voice that was (remediation.md 10, item 9).
/// <para>
/// The line used to carry the id alone, with a comment arguing that it is what this layer holds
/// and that opaque ids are still enough to tell two senders apart. Both true, and neither the
/// point: a Commander read "Spoken by D47 in JBFqnCBsd6RMkjVDRZzb" in their own log and could not
/// tell which voice that was. Telling two senders apart is not knowing who either of them is.
/// </para>
/// </summary>
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

    /// <summary>
    /// <b>And which service said it</b> (2026-08-28). A voice id means nothing without the
    /// provider that issued it, and since Phase 57 three of them can be speaking at once — so a
    /// line reading <em>Spoken by Boe Dock in pFQStpMdprGFILRDrWR2</em> named neither the voice
    /// nor whose it was. Asked of the client that actually spoke rather than of settings, which
    /// can have moved on since.
    /// </summary>
    [Fact]
    public async Task TheProviderIsNamedBesideTheVoice()
    {
        var said = await SpokenAsync(new VoiceSelection("pFQStpMdprGFILRDrWR2"), speaker: "Boe Dock");
        var line = Assert.Single(said, message => message.StartsWith("Spoken by", StringComparison.Ordinal));

        Assert.Contains(new FakeTtsProvider().Name, line, StringComparison.Ordinal);
    }

    /// <summary>
    /// And where nothing could resolve a name, the line is exactly what it always was rather than
    /// a gap or a repeated id. A catalogue that has not arrived yet is a normal state.
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
