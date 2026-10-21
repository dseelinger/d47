using System.Text;
using System.Text.Json;
using Xunit;

namespace D47.App.Tests;

/// <summary>Offline figures for the Whisper models over recorded commands (#1231).</summary>
public class MeasureWhisperModelsAgainstRecordedCommandsTests
{
    private const string DefaultIds = "tiny,tiny.en,base,base.en,small,small.en,medium,medium.en";

    private static string? Variable(string name) => Environment.GetEnvironmentVariable(name);

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheModelsAreMeasuredOverTheCorpus()
    {
        Assert.SkipUnless(Variable("D47_STT_COMPARE") == "1", "set D47_STT_COMPARE=1 to measure the models");

        var corpus = Variable("D47_STT_CORPUS");
        var models = Variable("D47_STT_MODELS");
        var output = Variable("D47_STT_OUT");

        Assert.False(string.IsNullOrWhiteSpace(corpus), "D47_STT_CORPUS names the corpus folder");
        Assert.False(string.IsNullOrWhiteSpace(models), "D47_STT_MODELS names the folder of ggml files");
        Assert.False(string.IsNullOrWhiteSpace(output), "D47_STT_OUT names the results folder");

        var ids = (Variable("D47_STT_IDS") ?? DefaultIds).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cpuSample = int.TryParse(Variable("D47_STT_CPU_SAMPLE"), out var sample) ? sample : 50;

        var results = await SttComparison.RunAsync(corpus, models, ids, cpuSample, output, TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ATwoClipCorpusIsMeasuredAndWrittenOut()
    {
        var models = Variable("D47_STT_MODELS");

        Assert.SkipUnless(
            models is not null && File.Exists(Path.Combine(models, "ggml-tiny.en.bin")),
            "set D47_STT_MODELS to a folder holding ggml-tiny.en.bin");

        var corpus = TempFolders.Create("d47-stt-corpus");
        var output = TempFolders.Create("d47-stt-out");

        File.WriteAllBytes(Path.Combine(corpus, "one.wav"), Wav(seconds: 1, tone: true));
        File.WriteAllBytes(Path.Combine(corpus, "two.wav"), Wav(seconds: 1, tone: false));

        File.WriteAllText(
            Path.Combine(corpus, "manifest.json"),
            JsonSerializer.Serialize(new[]
            {
                new { clip = "one.wav", text = "set volume to forty percent", language = "en", source = "recorded", routable = true },
                new { clip = "two.wav", text = "open the galaxy map", language = "en", source = "fleurs", routable = false },
            }));

        var results = await SttComparison.RunAsync(
            corpus, models!, ["tiny.en"], cpuSample: 50, output, TestContext.Current.CancellationToken);

        Assert.Contains(results, result => result.Clip == "one.wav" && result.Names);
        Assert.Contains(results, result => result.Clip == "two.wav");
        Assert.Contains("one.wav", File.ReadAllText(Path.Combine(output, "results.jsonl")), StringComparison.Ordinal);
        Assert.Contains("## tiny.en", File.ReadAllText(Path.Combine(output, "summary.md")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Set volume to forty percent.", "set volume to forty percent", 0)]
    [InlineData("set volume to forty percent", "set the volume to forty", 0.4)]
    [InlineData("", "", 0)]
    [InlineData("", "anything", 1)]
    public void WordErrorRateIgnoresCaseAndPunctuation(string reference, string heard, double expected) =>
        Assert.Equal(expected, SttComparison.WordErrorRate(reference, heard), precision: 6);

    private static byte[] Wav(int seconds, bool tone)
    {
        const int rate = 16_000;
        var count = rate * seconds;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write("RIFF"u8);
        writer.Write(36 + (count * 2));
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(count * 2);

        for (var i = 0; i < count; i++)
        {
            writer.Write((short)(tone ? Math.Sin(2 * Math.PI * 440 * i / rate) * 8000 : 0));
        }

        return stream.ToArray();
    }
}
