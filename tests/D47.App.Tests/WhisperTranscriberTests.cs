using D47.Stt;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The transcriber's failure path. Load never throws — no model is a state, not an error — and
/// when the native side refuses a load, the last things Whisper.net said are replayed into the
/// log at Error. That replay exists because the loader narrates the paths it probes at Debug,
/// below what the log keeps, which is how "Native Library not found" once shipped with nobody
/// able to see which paths had been tried.
/// </summary>
public class WhisperTranscriberTests
{
    [Fact]
    public void AGarbageModelFileFailsAsAStateAndReplaysTheNativeStory()
    {
        var log = new CapturingLogger();
        using var transcriber = new WhisperTranscriber(log);

        var garbage = Path.Combine(
            Directory.CreateTempSubdirectory("d47-stt-tests").FullName, "ggml-garbage.bin");
        File.WriteAllText(garbage, "not a ggml model");

        try
        {
            Assert.False(transcriber.Load(garbage, "garbage", useGpu: false));
            Assert.False(transcriber.IsReady);
            Assert.NotNull(transcriber.Unavailable);

            // The whole point of the buffer: the failure's log entry carries what the native
            // side was saying when it happened, at a level the log file actually keeps.
            Assert.Contains(
                log.Errors,
                line => line.Contains("What Whisper.net reported", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(garbage);
        }
    }

    [Fact]
    public void AMissingModelFileFailsWithoutTouchingTheNativeSide()
    {
        using var transcriber = new WhisperTranscriber(new CapturingLogger());

        Assert.False(transcriber.Load(@"C:\nowhere\ggml-none.bin", "none", useGpu: false));
        Assert.Contains("not on disk", transcriber.Unavailable);
    }

    private sealed class CapturingLogger : ILogger<WhisperTranscriber>
    {
        private readonly List<string> _errors = [];

        public IReadOnlyList<string> Errors
        {
            get
            {
                lock (_errors)
                {
                    return [.. _errors];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                lock (_errors)
                {
                    _errors.Add(formatter(state, exception));
                }
            }
        }
    }
}
