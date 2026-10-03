using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests;

public class TheShippedChatterboxVoicesAreUsableClipsTests
{
    [Fact]
    public void EveryShippedRowHasAClipOfTheRightShapeAndASource()
    {
        var folder = Path.Combine(RepositoryRoot(), "assets", "voices", "chatterbox");
        var log = new ListLogger();

        var voices = ChatterboxVoices.Load(folder, log);

        Assert.Empty(log.Messages);
        Assert.Equal(12, voices.Count);
        Assert.Equal(6, voices.Count(voice => voice.Voice.Gender == "female"));
        Assert.Equal(6, voices.Count(voice => voice.Voice.Gender == "male"));
        Assert.All(voices, voice => Assert.False(string.IsNullOrWhiteSpace(voice.Source)));
    }

    [Fact]
    public void AClipOfFourSecondsOrEightSecondsIsLeftOutAndTheLogNamesItsLength()
    {
        var folder = NewFolder();
        WriteClip(folder, "short", 4.0);
        WriteClip(folder, "long", 8.0);
        WriteClip(folder, "fine", 6.0);
        WriteTable(folder,
            "short\tShort\tfemale\ten\t\tsource",
            "long\tLong\tmale\ten\t\tsource",
            "fine\tFine\tmale\ten\tNarrator\tsource");
        var log = new ListLogger();

        var voices = ChatterboxVoices.Load(folder, log);

        var voice = Assert.Single(voices);
        Assert.Equal("fine", voice.Voice.Id);
        Assert.Equal(VoiceRole.Narrator, voice.Role);
        Assert.Contains(log.Messages, message => message.Contains("short") && message.Contains("4.0 s"));
        Assert.Contains(log.Messages, message => message.Contains("long") && message.Contains("8.0 s"));
    }

    [Fact]
    public void ARowWithNoSourceAMissingClipOrAWrongRateIsLeftOutWithItsReason()
    {
        var folder = NewFolder();
        WriteClip(folder, "nosource", 6.0);
        WriteClip(folder, "slow", 6.0, sampleRate: 16_000);
        WriteTable(folder,
            "nosource\tA\tfemale\ten\t\t",
            "missing\tB\tmale\ten\t\tsource",
            "slow\tC\tmale\ten\t\tsource");
        var log = new ListLogger();

        var voices = ChatterboxVoices.Load(folder, log);

        Assert.Empty(voices);
        Assert.Contains(log.Messages, message => message.Contains("nosource") && message.Contains("source is empty"));
        Assert.Contains(log.Messages, message => message.Contains("missing") && message.Contains("missing"));
        Assert.Contains(log.Messages, message => message.Contains("slow") && message.Contains("16000 Hz"));
    }

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void WriteTable(string folder, params string[] rows) =>
        File.WriteAllLines(
            Path.Combine(folder, ChatterboxVoices.TableName),
            ["id\tname\tgender\tlocale\trole\tsource", .. rows]);

    private static void WriteClip(string folder, string id, double seconds, int sampleRate = ChatterboxVoices.SampleRate)
    {
        var format = new AudioFormat(sampleRate, 1);
        var pcm = new byte[(int)(seconds * sampleRate) * format.BytesPerFrame];
        File.WriteAllBytes(Path.Combine(folder, id + ".wav"), WavWriter.ToBytes(pcm, format));
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
