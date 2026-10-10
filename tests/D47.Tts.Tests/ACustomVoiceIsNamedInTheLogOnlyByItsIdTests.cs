using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Tts.Tests;

[Trait("Category", "Integration")]
public sealed class ACustomVoiceIsNamedInTheLogOnlyByItsIdTests : IDisposable
{
    private const string Name = "Secret Ally";

    private readonly ChatterboxTestFolder _folder = new();
    private readonly Recorded _log = new();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _folder.Dispose();
    }

    private sealed class Recorded : ILogger<ChatterboxTtsProvider>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }

    private sealed class Engine : IChatterboxEngine
    {
        public IDisposable Encode(float[] reference) => new Handle();

        public float[] Speak(long[] textIds, IDisposable voice, CancellationToken cancellationToken) => new float[240];

        public void Dispose()
        {
        }

        private sealed class Handle : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public async Task TheStandInLineHasTheIdAndNotTheName()
    {
        var custom = new CustomVoices(Path.Combine(_folder.Root, "data"), new DpapiSecretProtector());

        Assert.Null(custom.Save(
            Name, "male", null, null, [.. Enumerable.Repeat(0.5f, ChatterboxVoices.SampleRate * 6)], ChatterboxVoices.SampleRate, out var id));

        using var provider = new ChatterboxTtsProvider(
            new DiskFileSystem(),
            _folder.Models, _folder.Voices, _folder.Fetched, _log, () => new Engine(), () => true, ChatterboxTestFolder.NoDownload, custom: custom);

        await provider.ListVoicesAsync(TestContext.Current.CancellationToken);
        custom.Delete(id!);
        await provider.SynthesizeAsync("hi", new VoiceSelection(id), TestContext.Current.CancellationToken);

        var line = Assert.Single(_log.Lines, text => text.Contains("spoken in", StringComparison.Ordinal));

        Assert.Contains(id!, line);
        Assert.DoesNotContain(_log.Lines, text => text.Contains(Name, StringComparison.OrdinalIgnoreCase));
    }
}
