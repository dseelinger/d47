using System.Diagnostics;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;

namespace ChatterboxProbe;

/// <summary>
/// What Chatterbox costs and whether it can be driven at all from C#, which is what #293 gates
/// everything else on. Nothing here is a wrapper's account of the model: the tokeniser, the audio
/// front end, the sampling loop and the KV cache are all written out, because the question is what
/// d47 would have to carry, not what Python can already do.
/// </summary>
internal static class Program
{
    private static string _root = @"C:\Users\dougs\AppData\Local\d47-spike\chatterbox";
    private static string _variant = "turbo";
    private static string _dtype = "q4f16";
    private static string _provider = "cpu";
    private static bool _providerGiven;
    private static string? _presentMon;
    private static int _maxTokens = 1000;
    private static float _penalty = 1.2f;
    private static bool _watch;
    private static int _seconds = 20;
    private static HashSet<string> _onCpu = [];

    private static string VariantRoot => Path.Combine(_root, _variant);

    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "help";
        var rest = new List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--root": _root = args[++i]; break;
                case "--variant": _variant = args[++i]; break;
                case "--dtype": _dtype = args[++i]; break;
                case "--provider": _provider = args[++i]; _providerGiven = true; break;
                case "--presentmon": _presentMon = args[++i]; break;
                case "--max-tokens": _maxTokens = int.Parse(args[++i]); break;
                case "--penalty": _penalty = float.Parse(args[++i]); break;
                case "--watch": _watch = true; break;
                case "--seconds": _seconds = int.Parse(args[++i]); break;
                case "--cpu-graphs": _onCpu = [.. args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries)]; break;
                default: rest.Add(args[i]); break;
            }
        }

        try
        {
            return command switch
            {
                "shape" => Shape(),
                "sizes" => Sizes(),
                "tokens" => Tokens(rest),
                "say" => Say(rest),
                "bench" => Bench(rest),
                "gpu" => ShowGpu(),
                "elite" => Elite(rest),
                _ => Help(),
            };
        }
        catch (OnnxRuntimeException exception)
        {
            Console.Error.WriteLine($"ONNX Runtime: {exception.Message}");
            return 1;
        }
        catch (FileNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int Help()
    {
        Console.WriteLine("""
            ChatterboxProbe — what Chatterbox costs through ONNX Runtime, from C#.

              shape                        the four graphs: files, inputs, outputs, bytes on disk
              sizes                        download size per published variant, from the Hub API
              tokens <text>                what the tokeniser makes of a line, tags included
              say <text> <ref.wav> <out>   one line end to end; first-sound latency and realtime
              bench <text> <ref.wav> [n]   the same line on each --provider, n runs each
              gpu                          the card, what it is holding, and which process holds it
              elite <text> <ref.wav>       Elite's frame time with and without a line being spoken

            --variant turbo|nano   --dtype fp32|fp16|q4|q4f16|q8   --provider cpu|dml
            --root <dir>   --max-tokens n   --penalty f   --watch   --presentmon <exe>
            --cpu-graphs speech_encoder,…   keep named graphs on the CPU whatever --provider says
            --seconds n            how long `elite` samples each of its two windows (default 20)
            """);

        return 0;
    }

    /// <summary>
    /// The interface, read rather than transcribed. It is the first thing to check against any
    /// future re-export: the pipeline is four graphs whose names and dtypes are load-bearing.
    /// </summary>
    private static int Shape()
    {
        using var pipeline = Pipeline.Open(VariantRoot, _dtype, _provider, _onCpu);

        Console.WriteLine($"variant   : {_variant} ({VariantRoot})");
        Console.WriteLine($"provider  : {_provider}");
        Console.WriteLine($"load      : {pipeline.LoadMs:F0} ms for all four sessions");
        Console.WriteLine();

        var total = 0L;

        foreach (var (graph, file, bytes) in pipeline.Graphs)
        {
            Console.WriteLine($"{graph,-21} {file,-38} {bytes / 1024 / 1024,6:N0} MB");
            total += bytes;
        }

        Console.WriteLine($"{"",-21} {"",-38} {total / 1024 / 1024,6:N0} MB total");

        Describe("embed_tokens", pipeline.Embed);
        Describe("speech_encoder", pipeline.Encoder);
        Describe("language_model", pipeline.Language);
        Describe("conditional_decoder", pipeline.Decoder);

        var tokeniser = Tokeniser.Load(Path.Combine(VariantRoot, "tokenizer.json"));

        Console.WriteLine();
        Console.WriteLine($"tags: {string.Join(" ", tokeniser.Tags)}");

        return 0;

        static void Describe(string name, InferenceSession session)
        {
            Console.WriteLine();
            Console.WriteLine($"{name}:");

            foreach (var input in session.InputMetadata.Take(4))
            {
                Console.WriteLine($"  in  {input.Key,-24} {input.Value.ElementDataType,-8} " +
                                  $"[{string.Join(",", input.Value.Dimensions)}]");
            }

            if (session.InputMetadata.Count > 4)
            {
                Console.WriteLine($"  in  … {session.InputMetadata.Count - 4} more");
            }

            foreach (var output in session.OutputMetadata.Take(4))
            {
                Console.WriteLine($"  out {output.Key,-24} {output.Value.ElementDataType,-8} " +
                                  $"[{string.Join(",", output.Value.Dimensions)}]");
            }

            if (session.OutputMetadata.Count > 4)
            {
                Console.WriteLine($"  out … {session.OutputMetadata.Count - 4} more");
            }
        }
    }

    /// <summary>
    /// The first-run download, per variant and per precision, straight from the Hub's file listing.
    /// A number rather than an impression, which is what #293 asks for: Kokoro's was 310 MB.
    /// </summary>
    private static int Sizes()
    {
        (string Label, string Repo)[] repositories =
        [
            ("turbo (official)", "ResembleAI/chatterbox-turbo-ONNX"),
            ("nano (community)", "owensong/chatterbox-nano-ONNX"),
            ("500M (community)", "onnx-community/chatterbox-ONNX"),
        ];

        string[] dtypes = ["fp32", "fp16", "q4", "q4f16", "quantized"];
        using var client = new HttpClient();

        foreach (var (label, repo) in repositories)
        {
            Console.WriteLine();
            Console.WriteLine($"{label}  {repo}");

            List<(string Path, long Size)> files;

            try
            {
                var json = client.GetStringAsync(
                    $"https://huggingface.co/api/models/{repo}/tree/main?recursive=true").Result;

                files = [.. JsonDocument.Parse(json).RootElement.EnumerateArray()
                    .Where(entry => entry.GetProperty("type").GetString() == "file")
                    .Select(entry => (
                        entry.GetProperty("path").GetString()!,
                        entry.TryGetProperty("size", out var size) ? size.GetInt64() : 0L))];
            }
            catch (Exception exception) when (exception is HttpRequestException or AggregateException)
            {
                Console.WriteLine("  (the Hub did not answer)");
                continue;
            }

            var support = files.Where(f => !f.Path.StartsWith("onnx/") && !f.Path.StartsWith("."))
                               .Sum(f => f.Size);

            foreach (var dtype in dtypes)
            {
                // fp32 is the *absence* of a suffix, not a suffix of its own, so it has to be
                // matched by elimination — EndsWith("") is true of every file there is.
                var set = files.Where(f => f.Path.StartsWith("onnx/") && (dtype == "fp32"
                                   ? dtypes.All(d => d == "fp32" ||
                                        !Graph(f.Path).EndsWith($"_{d}", StringComparison.Ordinal))
                                   : Graph(f.Path).EndsWith($"_{dtype}", StringComparison.Ordinal)))
                               .ToArray();

                if (set.Length == 0)
                {
                    continue;
                }

                var bytes = set.Sum(f => f.Size) + support;

                var graphs = set.Select(f => Graph(f.Path)).Distinct().Count();

                Console.WriteLine(
                    $"  {dtype,-10} {graphs,2} of 4 graphs  {bytes / 1024 / 1024,6:N0} MB");
            }

            Console.WriteLine($"  {"published",-10}    everything  " +
                              $"{files.Sum(f => f.Size) / 1024 / 1024,6:N0} MB");
        }

        Console.WriteLine();
        Console.WriteLine("Kokoro, for comparison: 310 MB.");

        return 0;

        // "onnx/language_model_q4f16.onnx_data" -> "language_model_q4f16"
        static string Graph(string path) =>
            Path.GetFileName(path).Replace(".onnx_data", string.Empty).Replace(".onnx", string.Empty);
    }

    private static int Tokens(List<string> args)
    {
        if (args.Count < 1)
        {
            Console.Error.WriteLine("tokens <text>");
            return 2;
        }

        var tokeniser = Tokeniser.Load(Path.Combine(VariantRoot, "tokenizer.json"));
        var ids = tokeniser.Encode(args[0]);

        Console.WriteLine($"{ids.Length} ids: {string.Join(" ", ids)}");
        Console.WriteLine($"tags known: {string.Join(" ", tokeniser.Tags)}");

        return 0;
    }

    private static int Say(List<string> args)
    {
        if (args.Count < 3)
        {
            Console.Error.WriteLine("say <text> <reference.wav> <out.wav>");
            return 2;
        }

        var reference = Reference(args[1]);
        var tokeniser = Tokeniser.Load(Path.Combine(VariantRoot, "tokenizer.json"));
        var ids = tokeniser.Encode(args[0]);

        using var pipeline = Pipeline.Open(VariantRoot, _dtype, _provider, _onCpu);
        using var watch = _watch ? new Gpu.Watch() : null;

        var (audio, timing) = pipeline.Speak(ids, reference, _maxTokens, _penalty);

        Audio.WriteWav(args[2], audio, Pipeline.SampleRate);

        Console.WriteLine(
            $"{_variant}/{_dtype} on {_provider}: {ids.Length} text tokens -> " +
            $"{timing.Tokens} speech tokens -> {timing.Seconds:F2}s of audio");
        Console.WriteLine(
            $"  load {timing.LoadMs,7:F0} ms   encode {timing.EncodeMs,6:F0} ms   " +
            $"language {timing.LanguageMs,7:F0} ms ({timing.MsPerToken:F1} ms/token)   " +
            $"decode {timing.DecodeMs,6:F0} ms");
        Console.WriteLine(
            $"  first sound {timing.TotalMs:F0} ms   realtime x{timing.Realtime:F2}   -> {args[2]}");

        if (watch is not null)
        {
            Console.WriteLine(
                $"  peak VRAM in use {watch.PeakUsedMb:N0} MB   peak GPU {watch.PeakUtilisation}%");
        }

        return 0;
    }

    /// <summary>
    /// The comparison the phase turns on: the same line, the same reference clip, on the CPU and on
    /// the GPU. One warm run first — the first inference pays for arena allocation and for the
    /// DirectML shader compile, neither of which is the cost of speaking a line.
    /// </summary>
    private static int Bench(List<string> args)
    {
        if (args.Count < 2)
        {
            Console.Error.WriteLine("bench <text> <reference.wav> [runs]");
            return 2;
        }

        var runs = args.Count > 2 ? int.Parse(args[2]) : 3;
        var reference = Reference(args[1]);
        var tokeniser = Tokeniser.Load(Path.Combine(VariantRoot, "tokenizer.json"));
        var ids = tokeniser.Encode(args[0]);

        // Both, unless told otherwise: the comparison is the point of the command.
        foreach (var provider in (_providerGiven ? _provider : "cpu,dml").Split(','))
        {
            Pipeline pipeline;

            // Before the sessions are opened, because on DirectML the weights land on the card at
            // load and a baseline taken afterwards would report the model as costing nothing.
            var baseline = Gpu.Read().FirstOrDefault()?.UsedMb ?? 0;

            try
            {
                pipeline = Pipeline.Open(VariantRoot, _dtype, provider, _onCpu);
            }
            catch (OnnxRuntimeException exception)
            {
                Console.WriteLine($"{provider,-4} unavailable: {Line(exception.Message)}");
                continue;
            }

            using (pipeline)
            {
                using var watch = new Gpu.Watch();

                var first = pipeline.Speak(ids, reference, _maxTokens, _penalty);
                var times = new List<double>();

                for (var run = 0; run < runs; run++)
                {
                    times.Add(pipeline.Speak(ids, reference, _maxTokens, _penalty).Timing.TotalMs);
                }

                times.Sort();

                var median = times[times.Count / 2];
                var seconds = first.Timing.Seconds;

                Console.WriteLine(
                    $"{provider,-4} load {pipeline.LoadMs,7:F0} ms   " +
                    $"warm {first.Timing.TotalMs,7:F0} ms   " +
                    $"median {median,7:F0} ms   min {times[0],7:F0}   max {times[^1],7:F0}   " +
                    $"audio {seconds,5:F2}s   realtime x{seconds * 1000 / median:F2}" +
                    // Only on the GPU: on the CPU this column reads whatever the desktop happened
                    // to do while the line was being spoken, which is not a fact about the model.
                    (provider == "dml"
                        ? $"   VRAM +{Math.Max(0, watch.PeakUsedMb - baseline):N0} MB"
                        : string.Empty));
            }
        }

        return 0;

        static string Line(string message) => message.Split('\n')[0].Trim();
    }

    private static int ShowGpu()
    {
        foreach (var reading in Gpu.Read())
        {
            Console.WriteLine(
                $"{reading.Name}: {reading.UsedMb:N0} of {reading.TotalMb:N0} MB in use, " +
                $"{reading.UtilisationPercent}% busy");
        }

        Console.WriteLine();

        foreach (var (process, pid, mb) in Gpu.PerProcess())
        {
            Console.WriteLine($"  {process,-28} pid {pid,-8} {mb,8:N0} MB dedicated");
        }

        return 0;
    }

    /// <summary>
    /// The measurement #293's amended ruling asks for: not whether the card has room, but whether
    /// the Commander can tell. Elite has to be running and drawing, on the surface being judged.
    /// </summary>
    private static int Elite(List<string> args)
    {
        const string Process = "EliteDangerous64.exe";

        var running = System.Diagnostics.Process.GetProcessesByName("EliteDangerous64").Length > 0;

        Console.WriteLine(running
            ? "Elite is running."
            : "Elite is NOT running — the numbers below are the desktop's, not the game's.");

        if (_presentMon is null)
        {
            Console.WriteLine(
                "No --presentmon <exe>: frame time cannot be measured. Everything else still can.");
        }

        var quiet = Sample(null);

        if (args.Count >= 2)
        {
            var reference = Reference(args[1]);
            var tokeniser = Tokeniser.Load(Path.Combine(VariantRoot, "tokenizer.json"));
            var ids = tokeniser.Encode(args[0]);

            using var pipeline = Pipeline.Open(VariantRoot, _dtype, _provider, _onCpu);

            pipeline.Speak(ids, reference, _maxTokens, _penalty);

            var stop = new CancellationTokenSource();
            var speaking = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    pipeline.Speak(ids, reference, _maxTokens, _penalty);
                }
            });

            var busy = Sample($"speaking on {_provider}");

            stop.Cancel();
            speaking.Wait();
            stop.Dispose();

            if (quiet is not null && busy is not null)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"frame time: {quiet.MeanMs:F2} ms quiet -> {busy.MeanMs:F2} ms speaking " +
                    $"({(busy.MeanMs - quiet.MeanMs) / quiet.MeanMs * 100:+0.0;-0.0}%), " +
                    $"99th {quiet.P99Ms:F2} -> {busy.P99Ms:F2} ms");
            }
        }

        return 0;

        Gpu.Frames? Sample(string? during)
        {
            var label = during ?? "quiet";
            var card = Gpu.Read().FirstOrDefault();
            var elite = Gpu.PerProcess().FirstOrDefault(p => p.Process == "EliteDangerous64");

            Console.WriteLine();
            Console.WriteLine($"--- {label}");
            Console.WriteLine($"card      : {card?.UsedMb ?? 0:N0} MB in use, {card?.UtilisationPercent ?? 0}% busy");
            Console.WriteLine($"Elite VRAM: {elite.Mb:N0} MB dedicated");

            if (_presentMon is null)
            {
                Thread.Sleep(_seconds * 1000);
                return null;
            }

            var frames = Gpu.FrameTimes(_presentMon, Process, _seconds);

            Console.WriteLine(frames is null
                ? "frames    : PresentMon returned nothing"
                : $"frames    : {frames.Count:N0} in {_seconds}s, mean {frames.MeanMs:F2} ms " +
                  $"({1000 / frames.MeanMs:F0} fps), 95th {frames.P95Ms:F2}, 99th {frames.P99Ms:F2}, " +
                  $"worst {frames.WorstMs:F2}");

            return frames;
        }
    }

    /// <summary>The reference clip, downmixed and resampled to what the speech encoder wants.</summary>
    private static float[] Reference(string path)
    {
        var (samples, rate) = Audio.ReadWav(path);
        var resampled = Audio.Resample(samples, rate, Pipeline.SampleRate);

        Console.WriteLine(
            $"reference : {Path.GetFileName(path)}  {samples.Length / (double)rate:F1}s at {rate} Hz" +
            (rate == Pipeline.SampleRate ? string.Empty : $" -> {Pipeline.SampleRate} Hz"));

        return resampled;
    }
}
