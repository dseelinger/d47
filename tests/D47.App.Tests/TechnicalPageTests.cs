using D47.App.Logging;
using D47.App.Panel;
using D47.Core.Audio;
using Serilog;
using Serilog.Events;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Technical page carries the speech loop as it happens, and the errors from it
/// (docs/plans/change-requests.md item 6).
/// <para>
/// The page is defined as "the same conversation, with the diagnostics left in", and five
/// turn-level appends were the whole of it. Everything the loop reported went to a log file
/// instead — information that existed, on the wrong surface.
/// </para>
/// </summary>
public class TechnicalPageTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static string Technical(PanelViewModel panel) =>
        string.Concat(panel.Segments(TranscriptPage.Technical).Select(segment => segment.Text));

    [Fact]
    public void EveryStageOfTheLoopIsWrittenDown()
    {
        var panel = new PanelViewModel();
        var trace = new SpeechLoopTrace(panel, () => Noon);

        foreach (var state in (LoopState[])
                 [LoopState.Listening, LoopState.Transcribing, LoopState.Thinking, LoopState.Speaking, LoopState.Answered])
        {
            trace.Entered(state);
        }

        var page = Technical(panel);

        Assert.Contains("listening", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("into words", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Working on an answer", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Speaking", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Answered", page, StringComparison.OrdinalIgnoreCase);

        // Ordered, and stamped from the App side — Core owns the loop and reads no clock.
        Assert.Contains("[12:00:00]", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// Idle is entered after every turn, and the same state can be re-entered. Neither is worth
    /// a line: one would double the page to say nothing was happening, and the other reads as a
    /// stall rather than as steady state.
    /// </summary>
    [Fact]
    public void RestAndRepetitionAreNotWorthALine()
    {
        var panel = new PanelViewModel();
        var trace = new SpeechLoopTrace(panel, () => Noon);

        trace.Entered(LoopState.Idle);
        Assert.Empty(Technical(panel));

        trace.Entered(LoopState.Thinking);
        var once = Technical(panel);

        trace.Entered(LoopState.Thinking);
        Assert.Equal(once, Technical(panel));

        trace.Entered(LoopState.Idle);
        Assert.Equal(once, Technical(panel));
    }

    /// <summary>
    /// Appended rather than rewritten in place. The stage before a failure staying on screen
    /// underneath it is the whole value of putting this on a transcript — the panel already has
    /// a control of the other kind, and it answers what is true now rather than what happened.
    /// </summary>
    [Fact]
    public void TheStagesStayOnThePageRatherThanReplacingEachOther()
    {
        var panel = new PanelViewModel();
        var trace = new SpeechLoopTrace(panel, () => Noon);

        trace.Entered(LoopState.Transcribing);
        trace.Entered(LoopState.Thinking);
        trace.Entered(LoopState.Failed);

        var page = Technical(panel);

        Assert.Contains("into words", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Working on an answer", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("could not be completed", page, StringComparison.OrdinalIgnoreCase);
    }

    private static ILogger Bridged(TechnicalLogBridge bridge) =>
        new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(bridge).CreateLogger();

    /// <summary>
    /// An error raised anywhere in the speech path reaches the page, including from a call site
    /// nobody has written yet. That is the whole reason this is a bridge rather than more
    /// authored lines.
    /// </summary>
    [Fact]
    public void AnErrorFromTheSpeechPathReachesThePage()
    {
        var panel = new PanelViewModel();
        var bridge = new TechnicalLogBridge();
        bridge.WriteTo(line => panel.Append(line + "\n", TranscriptKind.Technical));

        Bridged(bridge)
            .ForContext(Serilog.Core.Constants.SourceContextPropertyName, "D47.Core.Audio.SpeechPipeline")
            .Error(new InvalidOperationException("device in use"), "Could not start capture");

        var page = Technical(panel);

        Assert.Contains("Could not start capture", page, StringComparison.Ordinal);

        // The cause as well as the sentence: "could not start capture" without "device in use"
        // is one line short of being actionable.
        Assert.Contains("device in use", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// Errors only, and only from the speech path. Without both filters this is the Log tab
    /// twice, and a page that repeats another page is one nobody reads.
    /// </summary>
    [Theory]
    [InlineData("D47.Core.Audio.CueLibrary", LogEventLevel.Error, true)]
    [InlineData("D47.App.Voice.VoicePipeline", LogEventLevel.Error, true)]
    [InlineData("D47.Core.Listening.ListenGate", LogEventLevel.Fatal, true)]
    [InlineData("D47.Core.Audio.CueLibrary", LogEventLevel.Warning, false)]
    [InlineData("D47.Core.Audio.CueLibrary", LogEventLevel.Information, false)]
    [InlineData("D47.Core.Journal.JournalReader", LogEventLevel.Error, false)]
    [InlineData("D47.App.AppHost", LogEventLevel.Error, false)]
    public void OnlySpeechErrorsGetThrough(string source, LogEventLevel level, bool expected)
    {
        var panel = new PanelViewModel();
        var bridge = new TechnicalLogBridge();
        bridge.WriteTo(line => panel.Append(line + "\n", TranscriptKind.Technical));

        Bridged(bridge)
            .ForContext(Serilog.Core.Constants.SourceContextPropertyName, source)
            .Write(level, "a distinctive sentence");

        Assert.Equal(expected, Technical(panel).Contains("a distinctive sentence", StringComparison.Ordinal));
    }

    /// <summary>
    /// Logging is composed before there is a panel, because everything built after it needs
    /// somewhere to report a failure. Until the bridge is pointed at one it drops what it is
    /// given rather than throwing inside the logging pipeline.
    /// </summary>
    [Fact]
    public void BeforeThereIsAPanelItDropsQuietly()
    {
        var bridge = new TechnicalLogBridge();

        Bridged(bridge)
            .ForContext(Serilog.Core.Constants.SourceContextPropertyName, "D47.Core.Audio.SpeechPipeline")
            .Error("nothing is listening yet");
    }

    /// <summary>
    /// A sink that throws takes the logging pipeline down with it, and every other subsystem's
    /// ability to report anything. The page is the least important consumer of the event; the
    /// file already has it.
    /// </summary>
    [Fact]
    public void APanelThatThrowsDoesNotTakeLoggingWithIt()
    {
        var bridge = new TechnicalLogBridge();
        bridge.WriteTo(_ => throw new ObjectDisposedException("panel"));

        Bridged(bridge)
            .ForContext(Serilog.Core.Constants.SourceContextPropertyName, "D47.Core.Audio.SpeechPipeline")
            .Error("the window went away mid-turn");
    }

    /// <summary>
    /// The bridge writes from whichever thread raised the event — an audio thread — while the
    /// turn path is streaming a reply onto the same transcript. Before this feature there was one
    /// writer; a <see cref="System.Text.StringBuilder"/> read while another thread appends throws
    /// out of ToString, which would be a crash inside a diagnostics feature.
    /// </summary>
    [Fact]
    public async Task TheTranscriptSurvivesTwoThreadsWritingToIt()
    {
        var panel = new PanelViewModel();

        var writers = Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 250; i++)
            {
                panel.Append(
                    $"{worker}",
                    worker % 2 == 0 ? TranscriptKind.Technical : TranscriptKind.Conversation);
            }
        }));

        await Task.WhenAll(writers);

        Assert.Equal(1000, panel.TranscriptText.Length);
    }
}
