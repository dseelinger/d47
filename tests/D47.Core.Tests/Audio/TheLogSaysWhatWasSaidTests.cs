using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// What was said, beside who said it (GitHub issue 46).
/// <para>
/// Asked for after a Commander heard their carrier's tower say something wrong and could not find
/// it in the log. <c>CalloutEngine</c> had recorded the line it wrote — <em>"No fire zone
/// exited"</em> — and the announcement was then handed to a model to be reworded, which is what
/// reached the speaker. <b>The two lines in the log a second apart were the input and the
/// speaker, and nothing between them said the text had changed.</b>
/// </para>
/// <para>
/// Here rather than at any caller because this is where everything audible converges: a turn's
/// reply, a callout, a re-voiced in-game message, a crew member and a core's introduction all
/// build one of these. Several of those callers log nothing at all, so no other place could
/// answer "what did it just say".
/// </para>
/// </summary>
public class TheLogSaysWhatWasSaidTests
{
    private static Task<IReadOnlyList<string>> SpokenAsync(string? speaker, params string[] pushes) =>
        SpokenAsync(new FakeTtsProvider(), speaker, pushes);

    private static async Task<IReadOnlyList<string>> SpokenAsync(
        ITtsProvider tts,
        string? speaker,
        params string[] pushes)
    {
        var log = new List<string>();
        var arbiter = new AudioArbiter(
            new RecordingAudioSink(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AudioArbiter>.Instance).Start();

        await using (var speech = new SpeechPipeline(
            arbiter,
            tts,
            new VoiceSelection("voice-1") { Name = "George" },
            "test",
            new Capture(log),
            speaker: speaker))
        {
            foreach (var push in pushes)
            {
                speech.Push(push);
            }

            await speech.CompleteAsync();
        }

        return log;
    }

    private static string SaidLine(IReadOnlyList<string> log) =>
        Assert.Single(log, message => message.Contains(" said: ", StringComparison.Ordinal));

    /// <summary>The line the Commander could not find, findable.</summary>
    [Fact]
    public async Task WhatWasSpokenIsWrittenDown()
    {
        var line = SaidLine(await SpokenAsync("Sacred Fire BNH-T2F", "No fire zone exited."));

        Assert.Contains("Sacred Fire BNH-T2F said:", line, StringComparison.Ordinal);
        Assert.Contains("No fire zone exited.", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// One line per utterance rather than per sentence, and it carries the whole of what went
    /// out. Six lines for a six-sentence reply would bury everything around them, which is the
    /// same argument that keeps the voice line to one.
    /// </summary>
    [Fact]
    public async Task AWholeUtteranceIsOneLine()
    {
        var log = await SpokenAsync("D47", "Shields are down. ", "Hull is holding. ", "Get out of here.");
        var line = SaidLine(log);

        Assert.Contains("Shields are down.", line, StringComparison.Ordinal);
        Assert.Contains("Hull is holding.", line, StringComparison.Ordinal);
        Assert.Contains("Get out of here.", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// A caller with no name of its own reads as d47, matching the voice line beside it rather
    /// than inventing a second convention for the same absence.
    /// </summary>
    [Fact]
    public async Task AnUnnamedSpeakerIsD47()
    {
        Assert.Contains(
            "D47 said:",
            SaidLine(await SpokenAsync(speaker: null, "Acknowledged.")),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing spoken writes nothing. An utterance that was cut off before a single sentence
    /// rendered did not say anything, and a log line claiming it did would be the same class of
    /// wrong as the one this fixes.
    /// </summary>
    [Fact]
    public async Task SayingNothingWritesNothing()
    {
        var log = await SpokenAsync("D47");

        Assert.DoesNotContain(log, message => message.Contains(" said: ", StringComparison.Ordinal));
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

    /// <summary>
    /// <b>The delivery direction stays in this line and in no other</b> (#291, asked for on
    /// 2026-09-04 after reading it as a line of its own and preferring it in place).
    /// <para>
    /// Inline because where a tag sat is half of what it meant: <c>[dryly]</c> in front of the
    /// second of three sentences is a different reply from the same tag in front of the first, and
    /// a list beside the sentence cannot say which. This is the line a delivery complaint is read
    /// against, so it records what was <em>asked for</em> — never a claim that it was performed.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TheDirectionIsWrittenDownWhereItWasAskedFor()
    {
        var line = SaidLine(await SpokenAsync(
            new FakeTtsProvider { ReadsAudioTags = true },
            "D47",
            "Hull is holding. ",
            "[dryly] The correction is yours to make."));

        Assert.Contains("Hull is holding. [dryly] The correction is yours", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// And a provider that would read the brackets aloud has none to write down, because none was
    /// sent — the log says what went out, so for Flash and the local voice it says the words alone.
    /// </summary>
    [Fact]
    public async Task AProviderThatWasSentNoDirectionLogsNone()
    {
        var line = SaidLine(await SpokenAsync(
            new FakeTtsProvider { ReadsAudioTags = false },
            "D47",
            "[dryly] The correction is yours to make."));

        Assert.DoesNotContain("[dryly]", line, StringComparison.Ordinal);
        Assert.Contains("The correction is yours to make.", line, StringComparison.Ordinal);
    }
}
