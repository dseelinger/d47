using D47.Core.Audio;
using D47.Core.Speech;
using D47.Core.Storage;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>Speaks with the real Chatterbox Multilingual where it is on this machine: the installed app's copy, or the folder D47_CHATTERBOX_MULTILINGUAL_FOLDER names.</summary>
[Trait("Category", "Integration")]
public sealed class ChatterboxMultilingualLiveTests
{
    private static string Folder =>
        Environment.GetEnvironmentVariable("D47_CHATTERBOX_MULTILINGUAL_FOLDER")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "d47", "data", "models", "chatterbox-multilingual");

    private static string Dictionary => Path.Combine(AppContext.BaseDirectory, "dic");

    private static string Clip => Path.Combine(AppContext.BaseDirectory, "voices", "chatterbox", "marlow.wav");

    [Fact]
    public void AShortJapaneseLineStopsBeforeTheCap()
    {
        var files = new DiskFileSystem();

        Assert.SkipUnless(ChatterboxMultilingualAssets.IsInstalled(files, Folder), $"Chatterbox Multilingual is not in {Folder}");

        using var reader = new JapaneseReader(files, Dictionary);
        using var engine = ChatterboxMultilingualPipeline.Open(Folder, PerformanceCores.ForThisMachine());

        var tokeniser = ChatterboxMultilingualTokeniser.Load(
            files, ChatterboxMultilingualAssets.Destination(Folder, ChatterboxMultilingualAssets.Tokenizer));
        var ids = tokeniser.Encode(ChatterboxJapaneseText.Prepare("燃料は十分です。", reader.Read));

        Assert.DoesNotContain(1L, ids);

        using var voice = engine.Encode(PcmConverter.Pcm16ToFloats(WavReader.Read(files, Clip).Pcm.Span));

        var samples = engine.Speak(ids, voice, TestContext.Current.CancellationToken);
        var tokens = samples.Length / (ChatterboxPipeline.SampleRate / 25);

        Assert.True(samples.Length > ChatterboxPipeline.SampleRate / 2, $"only {samples.Length} samples");
        Assert.True(
            tokens < ChatterboxMultilingualPipeline.TokenLimit(ids.Length),
            $"{tokens} speech tokens reached the cap of {ChatterboxMultilingualPipeline.TokenLimit(ids.Length)}");
    }
}
