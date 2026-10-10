using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests;

[Trait("Category", "Gate")]
public class TheShippedChatterboxVoicesAreUsableClipsTests
{
    [Trait("Category", "Gate")]
    [Fact]
    public void EveryShippedRowHasAClipOfTheRightShapeAndASource()
    {
        var folder = Path.Combine(RepositoryRoot(), "assets", "voices", "chatterbox");
        var log = new ListLogger();

        var voices = ChatterboxVoices.Load(new DiskFileSystem(), folder, log);

        Assert.Empty(log.Messages);
        Assert.Equal(12, voices.Count);
        Assert.Equal(6, voices.Count(voice => voice.Voice.Gender == "female"));
        Assert.Equal(6, voices.Count(voice => voice.Voice.Gender == "male"));
        Assert.All(voices, voice => Assert.False(string.IsNullOrWhiteSpace(voice.Source)));
    }

    [Fact]
    public void AClipOfFourSecondsOrEightSecondsIsLeftOutAndTheLogNamesItsLength()
    {
        var files = new MemoryFileSystem();
        var folder = NewFolder();
        WriteClip(files, folder, "short", 4.0);
        WriteClip(files, folder, "long", 8.0);
        WriteClip(files, folder, "fine", 6.0);
        WriteTable(files, folder,
            "short\tShort\tfemale\ten\t\tsource",
            "long\tLong\tmale\ten\t\tsource",
            "fine\tFine\tmale\ten\tNarrator\tsource");
        var log = new ListLogger();

        var voices = ChatterboxVoices.Load(files, folder, log);

        var voice = Assert.Single(voices);
        Assert.Equal("fine", voice.Voice.Id);
        Assert.Equal(VoiceRole.Narrator, voice.Role);
        Assert.Contains(log.Messages, message => message.Contains("short") && message.Contains("4.0 s"));
        Assert.Contains(log.Messages, message => message.Contains("long") && message.Contains("8.0 s"));
    }

    [Fact]
    public void ARowWithNoSourceAMissingClipOrAWrongRateIsLeftOutWithItsReason()
    {
        var files = new MemoryFileSystem();
        var folder = NewFolder();
        WriteClip(files, folder, "nosource", 6.0);
        WriteClip(files, folder, "slow", 6.0, sampleRate: 16_000);
        WriteTable(files, folder,
            "nosource\tA\tfemale\ten\t\t",
            "missing\tB\tmale\ten\t\tsource",
            "slow\tC\tmale\ten\t\tsource");
        var log = new ListLogger();

        var voices = ChatterboxVoices.Load(files, folder, log);

        Assert.Empty(voices);
        Assert.Contains(log.Messages, message => message.Contains("nosource") && message.Contains("source is empty"));
        Assert.Contains(log.Messages, message => message.Contains("missing") && message.Contains("missing"));
        Assert.Contains(log.Messages, message => message.Contains("slow") && message.Contains("16000 Hz"));
    }

    private static string NewFolder() => "C:/d47-test/voices";

    private static void WriteTable(IFileSystem files, string folder, params string[] rows) =>
        files.WriteLines(
            Path.Combine(folder, ChatterboxVoices.TableName),
            ["id\tname\tgender\tlocale\trole\tsource", .. rows]);

    private static void WriteClip(MemoryFileSystem files, string folder, string id, double seconds, int sampleRate = ChatterboxVoices.SampleRate)
    {
        var format = new AudioFormat(sampleRate, 1);
        var pcm = new byte[(int)(seconds * sampleRate) * format.BytesPerFrame];
        files.WriteBytes(Path.Combine(folder, id + ".wav"), WavWriter.ToBytes(pcm, format));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
