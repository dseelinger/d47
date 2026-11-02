using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Tts.Tests;

public sealed class ACustomVoiceIsNamedInTheLogOnlyByItsIdTests
{
    private const string Name = "Secret Ally";

    private readonly ChatterboxTestFolder _folder = new();
    private readonly RecordingLogger<ChatterboxTtsProvider> _log = new();


    [Fact]
    public async Task TheStandInLineHasTheIdAndNotTheName()
    {
        var custom = new CustomVoices(Path.Combine(_folder.Root, "data"), _folder.Files, new DpapiSecretProtector());

        Assert.Null(custom.Save(
            Name, "male", null, null, [.. Enumerable.Repeat(0.5f, ChatterboxVoices.SampleRate * 6)], ChatterboxVoices.SampleRate, out var id));

        using var provider = new ChatterboxTtsProvider(
            _folder.Files,
            _folder.Models, _folder.Voices, _folder.Fetched, _log, () => new SilentChatterboxEngine(240), () => true, ChatterboxTestFolder.NoDownload, custom: custom);

        await provider.ListVoicesAsync(TestContext.Current.CancellationToken);
        custom.Delete(id!);
        await provider.SynthesizeAsync("hi", new VoiceSelection(id), TestContext.Current.CancellationToken);

        var line = Assert.Single(_log.Lines, text => text.Contains("spoken in", StringComparison.Ordinal));

        Assert.Contains(id!, line);
        Assert.DoesNotContain(_log.Lines, text => text.Contains(Name, StringComparison.OrdinalIgnoreCase));
    }
}
