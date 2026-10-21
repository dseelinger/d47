using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using D47.Core.Conversation;
using D47.Stt;
using Whisper.net;
using Whisper.net.LibraryLoader;
using Xunit;

namespace D47.App.Tests;

/// <summary>One clip in a comparison corpus's <c>manifest.json</c>.</summary>
internal sealed record SttClip(string Clip, string Text, string Language, string Source, bool Routable);

/// <summary>One clip run through one model in one configuration.</summary>
internal sealed record SttResult(
    string Model,
    string Clip,
    string Language,
    string Source,
    string Mode,
    string Device,
    bool Names,
    string Transcript,
    string? Detected,
    double WordErrorRate,
    bool Routable,
    string? RoutedTranscript,
    string? RoutedExpected,
    bool? RoutingAgrees,
    double ElapsedSeconds,
    double ClipSeconds,
    long ModelBytes);

/// <summary>Runs ggml models over a corpus of recorded clips, with the configuration d47 ships.</summary>
[Trait("Category", "Integration")]
internal static class SttComparison
{
    private const string Forced = "forced";
    private const string Automatic = "auto";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>The names prompt, as d47 builds it from the ship's AI names and the game state it knows.</summary>
    private static readonly string NamesPrompt = WhisperTranscriber.Vocabulary(
        ["D47", "Directive 47", "Sol", "Colonia", "Jameson Memorial", "Sidewinder", "Anaconda"])!;

    public static async Task<IReadOnlyList<SttResult>> RunAsync(
        string corpus,
        string models,
        IReadOnlyList<string> ids,
        int cpuSample,
        string output,
        CancellationToken cancellationToken)
    {
        var clips = JsonSerializer.Deserialize<List<SttClip>>(
                File.ReadAllText(Path.Combine(corpus, "manifest.json")), Json)
            ?? throw new InvalidOperationException("manifest.json holds no clips.");

        var router = new KeywordRouter(TestSurface.CreateFull().Registry);

        var onCpu = clips
            .GroupBy(clip => (clip.Language, clip.Source))
            .SelectMany(group => group.Take(cpuSample))
            .ToHashSet();

        RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];

        Directory.CreateDirectory(output);
        var results = new List<SttResult>();

        foreach (var id in ids)
        {
            var path = Path.Combine(models, $"ggml-{id}.bin");
            var multilingual = !id.EndsWith(".en", StringComparison.Ordinal);
            var size = new FileInfo(path).Length;

            foreach (var device in new[] { "gpu", "cpu" })
            {
                using var factory = WhisperFactory.FromPath(path, new WhisperFactoryOptions { UseGpu = device == "gpu" });
                var processors = new Dictionary<(string Language, bool Names), WhisperProcessor>();

                try
                {
                    foreach (var clip in clips.Where(clip => device == "gpu" || onCpu.Contains(clip)))
                    {
                        if (!multilingual && clip.Language != "en")
                        {
                            continue;
                        }

                        var samples = ReadWav(Path.Combine(corpus, clip.Clip));
                        var modes = multilingual ? new[] { Forced, Automatic } : [Forced];

                        foreach (var mode in modes)
                        {
                            var language = mode == Forced ? clip.Language : "auto";

                            foreach (var names in clip.Routable ? new[] { false, true } : [false])
                            {
                                if (!processors.TryGetValue((language, names), out var processor))
                                {
                                    processor = WhisperTranscriber.Processor(
                                        factory, names ? NamesPrompt : null, language);
                                    processors[(language, names)] = processor;
                                }

                                results.Add(await MeasureAsync(
                                    router, processor, clip, samples, id, mode, device, names, size, cancellationToken)
                                    .ConfigureAwait(false));
                            }
                        }
                    }
                }
                finally
                {
                    foreach (var processor in processors.Values)
                    {
                        processor.Dispose();
                    }
                }
            }
        }

        File.WriteAllLines(
            Path.Combine(output, "results.jsonl"),
            results.Select(result => JsonSerializer.Serialize(result, Json)));

        File.WriteAllText(Path.Combine(output, "summary.md"), Summary(results));

        return results;
    }

    private static async Task<SttResult> MeasureAsync(
        KeywordRouter router,
        WhisperProcessor processor,
        SttClip clip,
        float[] samples,
        string model,
        string mode,
        string device,
        bool names,
        long size,
        CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        string? detected = null;
        var stopwatch = Stopwatch.StartNew();

        await foreach (var segment in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
        {
            text.Append(segment.Text);

            if (string.IsNullOrEmpty(detected) && !string.IsNullOrEmpty(segment.Language))
            {
                detected = segment.Language;
            }
        }

        stopwatch.Stop();

        var transcript = text.ToString().Trim();
        var routedTranscript = clip.Routable ? Route(router, transcript) : null;
        var routedExpected = clip.Routable ? Route(router, clip.Text) : null;

        return new SttResult(
            model,
            clip.Clip,
            clip.Language,
            clip.Source,
            mode,
            device,
            names,
            transcript,
            detected,
            WordErrorRate(clip.Text, transcript),
            clip.Routable,
            routedTranscript,
            routedExpected,
            clip.Routable ? routedTranscript == routedExpected : null,
            stopwatch.Elapsed.TotalSeconds,
            (double)samples.Length / SampleRate,
            size);
    }

    private const int SampleRate = 16_000;

    /// <summary>The capability that <c>TurnLoop</c> would reach for the text without the model, or null.</summary>
    internal static string? Route(KeywordRouter router, string text) =>
        router.MatchSetting(text)?.CapabilityId
        ?? router.MatchToolCommand(text)?.CapabilityId
        ?? router.Match(text, InputSource.Spoken)?.CapabilityId;

    /// <summary>Lower-cased words with punctuation removed.</summary>
    internal static string[] Words(string text) =>
        new string([.. text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    internal static double WordErrorRate(string reference, string hypothesis)
    {
        var expected = Words(reference);
        var heard = Words(hypothesis);

        if (expected.Length == 0)
        {
            return heard.Length == 0 ? 0 : 1;
        }

        var previous = Enumerable.Range(0, heard.Length + 1).ToArray();

        for (var i = 1; i <= expected.Length; i++)
        {
            var current = new int[heard.Length + 1];
            current[0] = i;

            for (var j = 1; j <= heard.Length; j++)
            {
                var substitution = previous[j - 1] + (expected[i - 1] == heard[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j], current[j - 1]) + 1);
            }

            previous = current;
        }

        return (double)previous[heard.Length] / expected.Length;
    }

    internal static string Summary(IReadOnlyList<SttResult> results)
    {
        var text = new StringBuilder("# Whisper comparison\n");
        var invariant = CultureInfo.InvariantCulture;

        foreach (var model in results.GroupBy(result => result.Model))
        {
            text.Append(invariant, $"\n## {model.Key} ({model.First().ModelBytes / 1_000_000} MB)\n\n");
            text.Append("| Language | Source | Mode | Device | Names | Clips | Mean WER | Routing agrees | Time / clip |\n");
            text.Append("|---|---|---|---|---|---|---|---|---|\n");

            foreach (var group in model
                         .GroupBy(result => (result.Language, result.Source, result.Mode, result.Device, result.Names))
                         .OrderBy(group => group.Key))
            {
                var routable = group.Where(result => result.RoutingAgrees is not null).ToList();

                var agreement = routable.Count == 0
                    ? "-"
                    : $"{routable.Count(result => result.RoutingAgrees == true)}/{routable.Count}";

                text.Append(
                    invariant,
                    $"| {group.Key.Language} | {group.Key.Source} | {group.Key.Mode} | {group.Key.Device} | "
                    + $"{(group.Key.Names ? "yes" : "no")} | {group.Count()} | "
                    + $"{group.Average(result => result.WordErrorRate):P1} | {agreement} | "
                    + $"{group.Sum(result => result.ElapsedSeconds) / group.Sum(result => result.ClipSeconds):0.00}x |\n");
            }
        }

        return text.ToString();
    }

    /// <summary>16 kHz, 16-bit PCM WAV as mono floats.</summary>
    internal static float[] ReadWav(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var channels = 1;
        var offset = 12;

        while (offset + 8 <= bytes.Length)
        {
            var name = Encoding.ASCII.GetString(bytes, offset, 4);
            var length = BitConverter.ToInt32(bytes, offset + 4);
            var body = offset + 8;

            if (name == "fmt ")
            {
                channels = BitConverter.ToInt16(bytes, body + 2);
                var rate = BitConverter.ToInt32(bytes, body + 4);
                var bits = BitConverter.ToInt16(bytes, body + 14);

                if (rate != SampleRate || bits != 16)
                {
                    throw new InvalidDataException($"{Path.GetFileName(path)} is not 16 kHz 16-bit PCM.");
                }
            }
            else if (name == "data")
            {
                var count = Math.Min(length, bytes.Length - body) / 2 / channels;
                var samples = new float[count];

                for (var i = 0; i < count; i++)
                {
                    var sum = 0f;

                    for (var c = 0; c < channels; c++)
                    {
                        sum += BitConverter.ToInt16(bytes, body + ((i * channels) + c) * 2) / 32768f;
                    }

                    samples[i] = sum / channels;
                }

                return samples;
            }

            offset = body + length + (length & 1);
        }

        throw new InvalidDataException($"{Path.GetFileName(path)} has no data chunk.");
    }
}
