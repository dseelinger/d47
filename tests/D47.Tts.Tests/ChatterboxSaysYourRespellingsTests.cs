using D47.Core.Storage;
using D47.Tts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

public sealed class ChatterboxSaysYourRespellingsTests
{
    private const string Pronunciations = "pronunciations.json";

    private static ChatterboxTtsProvider Provider(MemoryFileSystem files, ILogger<ChatterboxTtsProvider>? logger = null)
    {
        var folder = "chatterbox";

        return new ChatterboxTtsProvider(
            files,
            folder,
            folder,
            folder,
            logger ?? NullLogger<ChatterboxTtsProvider>.Instance,
            () => throw new InvalidOperationException("Nothing is spoken here."),
            () => true,
            ChatterboxTestFolder.NoDownload,
            pronunciations: Pronunciations);
    }

    private static MemoryFileSystem With(string json)
    {
        var files = new MemoryFileSystem();
        files.WriteText(Pronunciations, json);
        return files;
    }

    [Fact]
    public void ARespelledWordIsEncodedAsRespelled()
    {
        using var provider = Provider(With("""{"Supercruise": "super cruise"}"""));

        Assert.Equal("Preparing for super cruise.", provider.Respelled("Preparing for supercruise."));
    }

    [Fact]
    public void OnlyWholeWordsMatchAndTheLongestKeyWins()
    {
        using var provider = Provider(With("""{"male": "mail", "Shinrarta": "shin", "Shinrarta Dezhra": "shin rar tah"}"""));

        Assert.Equal("female and mail", provider.Respelled("female and male"));
        Assert.Equal("Go to shin rar tah, now", provider.Respelled("Go to Shinrarta Dezhra, now"));
    }

    [Fact]
    public void AnIpaEntryIsSkippedAndNamedOnce()
    {
        var log = new RecordingLogger();
        using var provider = Provider(With("""{"Dezhra": "ipa:ˈdɛʒɹə"}"""), log);

        Assert.Equal("Dezhra and Dezhra", provider.Respelled("Dezhra and Dezhra"));
        Assert.Equal("Dezhra", provider.Respelled("Dezhra"));
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void AnEditedFileTakesEffectWithoutARestart()
    {
        var files = With("""{"Deciat": "dessy at"}""");
        using var provider = Provider(files);

        Assert.Equal("dessy at", provider.Respelled("Deciat"));

        files.WriteText(Pronunciations, """{"Deciat": "deshy at", "padding": "to change the length"}""");

        Assert.Equal("deshy at", provider.Respelled("Deciat"));
    }

    [Fact]
    public void ALineWithNoMatchIsUntouched()
    {
        using var provider = Provider(With("""{"Supercruise": "super cruise"}"""));

        Assert.Equal("Docked  at\nthe station.", provider.Respelled("Docked  at\nthe station."));
    }

    private sealed class RecordingLogger : ILogger<ChatterboxTtsProvider>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
