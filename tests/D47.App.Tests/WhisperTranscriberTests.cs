using D47.Stt;
using Microsoft.Extensions.Logging;
using Whisper.net.LibraryLoader;
using Xunit;

namespace D47.App.Tests;

/// <summary>The transcriber's failure path.</summary>
public class WhisperTranscriberTests
{
    [Fact]
    public void AGarbageModelFileFailsAsAStateAndReplaysTheNativeStory()
    {
        var log = new CapturingLogger();
        using var transcriber = new WhisperTranscriber(log);

        var garbage = Path.Combine(
            TempFolders.Create("d47-stt-tests"), "ggml-garbage.bin");
        File.WriteAllText(garbage, "not a ggml model");

        try
        {
            Assert.False(transcriber.Load(garbage, "garbage", useGpu: false));
            Assert.False(transcriber.IsReady);
            Assert.NotNull(transcriber.Unavailable);

            // The whole point of the buffer: the failure's log entry carries what the native side was saying
            // when it happened, at a level the log file actually keeps.
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

    /// <summary>Four cores are left for the game, and both ends of the clamp are measured rather than
    /// chosen.</summary>
    [Theory]
    [InlineData(1, 4)]
    [InlineData(4, 4)]
    [InlineData(8, 4)]
    [InlineData(12, 8)]
    [InlineData(16, 12)]
    [InlineData(24, 16)]
    [InlineData(64, 16)]
    public void TheThreadCountLeavesFourCoresAndNeverGoesBelowWhisperSOwnDefault(int processors, int expected) =>
        Assert.Equal(expected, WhisperTranscriber.ThreadsFor(processors));

 /// <summary>"On the GPU" is a claim about what loaded, not about what was asked.</summary>
    [Theory]
    [InlineData(true, RuntimeLibrary.Cpu, false)]
    [InlineData(true, RuntimeLibrary.CpuNoAvx, false)]
    [InlineData(true, RuntimeLibrary.Cuda, true)]
    [InlineData(true, RuntimeLibrary.Cuda12, true)]
    [InlineData(true, RuntimeLibrary.Vulkan, true)]
    [InlineData(false, RuntimeLibrary.Cuda, false)]
    [InlineData(true, null, false)]
    public void TheGpuClaimNeedsBothTheRequestAndAGpuLibrary(
        bool requested, RuntimeLibrary? loaded, bool expected) =>
        Assert.Equal(expected, WhisperTranscriber.RunsOnGpu(requested, loaded));

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
