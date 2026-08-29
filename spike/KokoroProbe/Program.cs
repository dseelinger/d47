using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace KokoroProbe;

/// <summary>
/// What Kokoro actually does through the ONNX path, measured from C# rather than from a Python
/// wrapper's account of it — which is what the unbuilt Phase 59 issue asks for before any of
/// it is designed around.
/// </summary>
internal static class Program
{
    private const int SampleRate = 24000;

    private static string _root = @"C:\Users\dougs\AppData\Local\d47-spike\kokoro";

    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "help";

        var rootFlag = Array.IndexOf(args, "--root");
        if (rootFlag >= 0 && rootFlag + 1 < args.Length)
        {
            _root = args[rootFlag + 1];
        }

        return command switch
        {
            "shape" => Shape(),
            "g2pshape" => G2pShape(args),
            "g2p" => G2p(args),
            "g2peval" => G2pEval(args),
            "g2pframe" => G2pFrame(args),
            "speak" => Speak(args),
            "say" => Say(args),
            "blend" => Blend(args),
            "bench" => Bench(args),
            "quality" => Quality(args),
            _ => Help(),
        };
    }

    private static int Help()
    {
        Console.WriteLine("""
            KokoroProbe — what Kokoro does through ONNX Runtime, from C#.

              shape                      model inputs, outputs, and the voice tensor's real shape
              say <ipa> <voice> <out>    synthesise one line of IPA to a WAV
              blend <ipa> <a> <b> <t> <out>   the same line from a weighted blend of two voices
              bench <ipa> <voice> [n]    latency per line, across all eight published builds
              quality <ipa> <voice> [dir]   how far each build departs from fp32, and WAVs to hear

            IPA in, not text. The vocabulary is 115 IPA symbols and punctuation.
            """);

        return 0;
    }

    private static string ModelPath(bool quantised = false) =>
        Path.Combine(_root, "onnx", quantised ? "model_q8f16.onnx" : "model.onnx");

    /// <summary>
    /// The eight published ONNX builds, fp32 first (#139).
    /// <para>
    /// Ordered as the repository publishes them rather than by size, because size is exactly what
    /// this list exists to stop anybody reasoning from: <c>model_q4.onnx</c> is a <em>quantised</em>
    /// build and is 305 MB, within 6% of fp32.
    /// </para>
    /// </summary>
    private static readonly (string Label, string File)[] Builds =
    [
        ("fp32", "model.onnx"),
        ("fp16", "model_fp16.onnx"),
        ("q4", "model_q4.onnx"),
        ("q4f16", "model_q4f16.onnx"),
        ("q8f16", "model_q8f16.onnx"),
        ("quantized", "model_quantized.onnx"),
        ("uint8", "model_uint8.onnx"),
        ("uint8f16", "model_uint8f16.onnx"),
    ];

    /// <summary>
    /// The phoneme vocabulary, read from <c>tokenizer.json</c> rather than transcribed — a
    /// hand-copied vocabulary is a silent mismatch waiting to happen.
    /// </summary>
    private static Dictionary<string, long> Vocabulary()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "tokenizer.json")));

        var vocab = doc.RootElement.GetProperty("model").GetProperty("vocab");
        var map = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var entry in vocab.EnumerateObject())
        {
            map[entry.Name] = entry.Value.GetInt64();
        }

        return map;
    }

    /// <summary>
    /// IPA to token ids. By text element rather than by char, because several of these symbols are
    /// more than one UTF-16 unit and one — the nasal tilde — is a combining mark that belongs to
    /// the symbol before it.
    /// </summary>
    private static long[] Encode(string ipa, out List<string> unknown)
    {
        var vocab = Vocabulary();
        var ids = new List<long> { 0 };
        unknown = [];

        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(ipa);

        while (enumerator.MoveNext())
        {
            var symbol = (string)enumerator.Current;

            if (vocab.TryGetValue(symbol, out var id))
            {
                ids.Add(id);
                continue;
            }

            // A combining mark may have been folded into the element before it; try the base.
            var handled = false;

            foreach (var rune in symbol.EnumerateRunes())
            {
                if (vocab.TryGetValue(rune.ToString(), out var part))
                {
                    ids.Add(part);
                    handled = true;
                }
            }

            if (!handled)
            {
                unknown.Add(symbol);
            }
        }

        ids.Add(0);
        return [.. ids];
    }

    /// <summary>
    /// One voice's style tensor. 510 buckets of 256 floats: the bucket is chosen by how many
    /// tokens are being spoken, which is the detail that makes a "voice" a table rather than a
    /// vector.
    /// </summary>
    private static float[] Style(string voice, int tokens)
    {
        var bytes = File.ReadAllBytes(Path.Combine(_root, "voices", voice + ".bin"));
        var floats = new float[bytes.Length / 4];

        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);

        const int Dimensions = 256;
        var buckets = floats.Length / Dimensions;
        var bucket = Math.Clamp(tokens, 0, buckets - 1);

        return floats[(bucket * Dimensions)..((bucket + 1) * Dimensions)];
    }

    private static int Shape()
    {
        using var session = new InferenceSession(ModelPath());

        Console.WriteLine($"model     : {ModelPath()}");
        Console.WriteLine();
        Console.WriteLine("inputs:");

        foreach (var input in session.InputMetadata)
        {
            Console.WriteLine(
                $"  {input.Key,-12} {input.Value.ElementType.Name,-8} [{string.Join(",", input.Value.Dimensions)}]");
        }

        Console.WriteLine("outputs:");

        foreach (var output in session.OutputMetadata)
        {
            Console.WriteLine(
                $"  {output.Key,-12} {output.Value.ElementType.Name,-8} [{string.Join(",", output.Value.Dimensions)}]");
        }

        var voice = Path.Combine(_root, "voices", "af_heart.bin");
        var size = new FileInfo(voice).Length;

        Console.WriteLine();
        Console.WriteLine($"voice file: {size:N0} bytes = {size / 4:N0} floats");
        Console.WriteLine($"          = {size / 4 / 256:N0} buckets of 256, indexed by token count");

        var vocab = Vocabulary();
        Console.WriteLine($"vocabulary: {vocab.Count} symbols");

        return 0;
    }

    /// <summary>
    /// What the phonemiser's interface actually is — the question that decides whether d47 can do
    /// its own grapheme-to-phoneme step without the GPL one everything else reaches for.
    /// </summary>
    private static int G2pShape(string[] args)
    {
        var path = args.Length > 1
            ? args[1]
            : @"C:\Users\dougs\AppData\Local\d47-spike\phonemizer\model.onnx";

        using var session = new InferenceSession(path);

        Console.WriteLine($"model: {path} ({new FileInfo(path).Length / 1024 / 1024} MB)");
        Console.WriteLine("inputs:");

        foreach (var input in session.InputMetadata)
        {
            Console.WriteLine(
                $"  {input.Key,-14} {input.Value.ElementType.Name,-8} [{string.Join(",", input.Value.Dimensions)}]");
        }

        Console.WriteLine("outputs:");

        foreach (var output in session.OutputMetadata)
        {
            Console.WriteLine(
                $"  {output.Key,-14} {output.Value.ElementType.Name,-8} [{string.Join(",", output.Value.Dimensions)}]");
        }

        return 0;
    }

    private static string PhonemizerRoot { get; } =
        @"C:\Users\dougs\AppData\Local\d47-spike\phonemizer";

    /// <summary>
    /// The phonemiser's two vocabularies: characters in, IPA out.
    /// </summary>
    private static (Dictionary<char, long> Text, Dictionary<int, string> Phonemes) G2pVocab()
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(PhonemizerRoot, "tokenizer.json")));

        var text = new Dictionary<char, long>();

        foreach (var entry in doc.RootElement.GetProperty("text_symbols").EnumerateObject())
        {
            if (entry.Name.Length == 1)
            {
                text[entry.Name[0]] = entry.Value.GetInt64();
            }
        }

        var phonemes = new Dictionary<int, string>();

        foreach (var entry in doc.RootElement.GetProperty("phoneme_symbols").EnumerateObject())
        {
            phonemes[int.Parse(entry.Name)] = entry.Value.GetString() ?? "";
        }

        return (text, phonemes);
    }

    /// <summary>
    /// One word to IPA. A forward transformer, so this is argmax per input position rather than an
    /// autoregressive decode — one phoneme per letter, with the blanks dropped.
    /// </summary>
    private static string Phonemise(InferenceSession session, string word)
    {
        var (text, phonemes) = G2pVocab();

        // The language token leads and the end marker is left off: of the four framings g2pframe
        // tries, this is the least wrong, so it is the one the numbers are reported against.
        var ids = new List<long> { 1 };

        foreach (var c in word)
        {
            if (text.TryGetValue(c, out var id))
            {
                ids.Add(id);
            }
        }

        var input = new DenseTensor<long>(ids.ToArray(), [1, ids.Count]);

        using var results = session.Run(
            [NamedOnnxValue.CreateFromTensor("text", input)]);

        var logits = results.First().AsTensor<float>();
        var positions = logits.Dimensions[1];
        var classes = logits.Dimensions[2];

        var built = new StringBuilder();

        for (var p = 0; p < positions; p++)
        {
            var best = 0;

            for (var c = 1; c < classes; c++)
            {
                if (logits[0, p, c] > logits[0, p, best])
                {
                    best = c;
                }
            }

            var symbol = phonemes.GetValueOrDefault(best, "");

            // 0 is the pad, 1 the language tag, 2 the end marker: none of them is a sound.
            if (best > 2 && symbol.Length > 0)
            {
                built.Append(symbol);
            }
        }

        return built.ToString();
    }

    /// <summary>Every input position against the symbol the model puts there, to see the alignment
    /// rather than guess at it.</summary>
    private static void Dump(InferenceSession session, string word)
    {
        var (text, phonemes) = G2pVocab();

        var ids = new List<long> { 1 };
        var chars = new List<string> { "<en_us>" };

        foreach (var c in word)
        {
            if (text.TryGetValue(c, out var id))
            {
                ids.Add(id);
                chars.Add(c.ToString());
            }
        }

        ids.Add(2);
        chars.Add("<end>");

        var input = new DenseTensor<long>(ids.ToArray(), [1, ids.Count]);
        using var results = session.Run([NamedOnnxValue.CreateFromTensor("text", input)]);

        var logits = results.First().AsTensor<float>();

        Console.WriteLine($"    input {ids.Count} positions, output {logits.Dimensions[1]}");

        for (var p = 0; p < logits.Dimensions[1]; p++)
        {
            var best = 0;

            for (var c = 1; c < logits.Dimensions[2]; c++)
            {
                if (logits[0, p, c] > logits[0, p, best])
                {
                    best = c;
                }
            }

            var inChar = p < chars.Count ? chars[p] : "-";
            Console.WriteLine($"    {p,3}  {inChar,-8} -> {best,3} {phonemes.GetValueOrDefault(best, "?")}");
        }
    }

    /// <summary>
    /// How good the neural fallback is, scored against the dictionary's own answers.
    /// <para>
    /// <b>This is an upper bound rather than an estimate</b>, and the reason matters: the model was
    /// almost certainly trained on this very dictionary, so every word scored here is likely
    /// training data. A real Elite system name is not. If the fallback is weak even on words it was
    /// taught, that settles the question in the direction that cannot be argued with.
    /// </para>
    /// </summary>
    /// <summary>
    /// Four ways of framing the input, because "the model is bad" and "I am feeding it wrong" look
    /// identical from the outside and only one of them is worth writing down.
    /// </summary>
    private static int G2pFrame(string[] args)
    {
        var word = args.Length > 1 ? args[1] : "commander";

        Console.OutputEncoding = Encoding.UTF8;

        var (text, phonemes) = G2pVocab();
        using var session = new InferenceSession(Path.Combine(PhonemizerRoot, "model.onnx"));

        foreach (var (label, lang, end) in new[]
                 {
                     ("lang + end", true, true),
                     ("lang only ", true, false),
                     ("end only  ", false, true),
                     ("bare      ", false, false),
                 })
        {
            var ids = new List<long>();

            if (lang)
            {
                ids.Add(1);
            }

            foreach (var c in word)
            {
                if (text.TryGetValue(c, out var id))
                {
                    ids.Add(id);
                }
            }

            if (end)
            {
                ids.Add(2);
            }

            var input = new DenseTensor<long>(ids.ToArray(), [1, ids.Count]);
            using var results = session.Run([NamedOnnxValue.CreateFromTensor("text", input)]);
            var logits = results.First().AsTensor<float>();

            var built = new StringBuilder();

            for (var p = 0; p < logits.Dimensions[1]; p++)
            {
                var best = 0;

                for (var c = 1; c < logits.Dimensions[2]; c++)
                {
                    if (logits[0, p, c] > logits[0, p, best])
                    {
                        best = c;
                    }
                }

                if (best > 2)
                {
                    built.Append(phonemes.GetValueOrDefault(best, ""));
                }
            }

            Console.WriteLine($"  {label}  {built}");
        }

        return 0;
    }

    private static int G2pEval(string[] args)
    {
        var sample = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 300;

        Console.OutputEncoding = Encoding.UTF8;

        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(PhonemizerRoot, "phoneme_dict.json")));

        var entries = doc.RootElement.GetProperty("en_us")
            .EnumerateObject()
            .Where(e => e.Name.All(char.IsLetter) && e.Name.Length > 3)
            .Select(e => (Word: e.Name, Ipa: e.Value.GetString() ?? ""))
            .ToList();

        // Deterministic, so a re-run compares against the same words.
        var random = new Random(47);
        var picked = entries.OrderBy(_ => random.Next()).Take(sample).ToList();

        using var session = new InferenceSession(Path.Combine(PhonemizerRoot, "model.onnx"));

        var exact = 0;
        var distances = new List<double>();
        var shown = 0;

        foreach (var (word, truth) in picked)
        {
            var got = Phonemise(session, word);

            if (got == truth)
            {
                exact++;
            }
            else if (shown < 12)
            {
                Console.WriteLine($"  {word,-18} want {truth,-22} got {got}");
                shown++;
            }

            distances.Add(Levenshtein(got, truth) / (double)Math.Max(truth.Length, 1));
        }

        distances.Sort();

        Console.WriteLine();
        Console.WriteLine($"{picked.Count} words sampled from the dictionary the model was trained on.");
        Console.WriteLine($"  exact match      : {100.0 * exact / picked.Count:F1}%");
        Console.WriteLine($"  median error     : {distances[distances.Count / 2] * 100:F1}% of symbols");
        Console.WriteLine($"  90th percentile  : {distances[(int)(distances.Count * 0.9)] * 100:F1}%");

        return 0;
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    private static int G2p(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("g2p <word> [word...]");
            return 2;
        }

        using var session = new InferenceSession(Path.Combine(PhonemizerRoot, "model.onnx"));

        Console.OutputEncoding = Encoding.UTF8;

        var raw = args.Contains("--raw");

        foreach (var word in args[1..].Where(a => !a.StartsWith("--")))
        {
            Console.WriteLine($"{word,-24} {Phonemise(session, word)}");

            if (raw)
            {
                Dump(session, word);
            }
        }

        return 0;
    }

    /// <summary>
    /// The whole road, which is the thing worth proving: plain text in, phonemes worked out here,
    /// audio out — with no GPL phonemiser and no Python anywhere in it.
    /// </summary>
    private static int Speak(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("speak <text> <voice> <out.wav>");
            return 2;
        }

        Console.OutputEncoding = Encoding.UTF8;

        var watch = Stopwatch.StartNew();
        using var g2p = new InferenceSession(Path.Combine(PhonemizerRoot, "model.onnx"));
        using var tts = new InferenceSession(ModelPath());
        var loaded = watch.ElapsedMilliseconds;

        watch.Restart();

        // Dictionary first, net second - which is how OpenPhonemizer is meant to be used, and the
        // order matters enormously here: the dictionary is exact and the net is not.
        using var dict = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(PhonemizerRoot, "phoneme_dict.json")));

        var lookup = dict.RootElement.GetProperty("en_us");

        var ipa = new StringBuilder();
        var guessed = new List<string>();

        foreach (var token in args[1].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var trailing = new string(token.Where(char.IsPunctuation).ToArray());
            var word = new string(token.Where(char.IsLetter).ToArray());

            if (word.Length > 0)
            {
                if (lookup.TryGetProperty(word.ToLowerInvariant(), out var known))
                {
                    ipa.Append(known.GetString());
                }
                else
                {
                    guessed.Add(word);
                    ipa.Append(Phonemise(g2p, word));
                }
            }

            ipa.Append(trailing).Append(' ');
        }

        var phonemes = ipa.ToString().Trim();
        var phonemised = watch.ElapsedMilliseconds;

        watch.Restart();
        var ids = Encode(phonemes, out var unknown);
        var audio = Synthesise(tts, ids, Style(args[2], ids.Length));
        var spoke = watch.ElapsedMilliseconds;

        WriteWav(args[3], audio);

        Console.WriteLine($"text   : {args[1]}");
        Console.WriteLine($"ipa    : {phonemes}");

        if (guessed.Count > 0)
        {
            Console.WriteLine($"guessed: {string.Join(" ", guessed)}  (not in the dictionary)");
        }

        if (unknown.Count > 0)
        {
            Console.WriteLine($"unknown: {string.Join(" ", unknown.Distinct())}");
        }

        Console.WriteLine($"load {loaded} ms   g2p {phonemised} ms   synth {spoke} ms   " +
                          $"{audio.Length / (double)SampleRate:F2}s -> {args[3]}");

        return 0;
    }

    private static float[] Synthesise(
        InferenceSession session, long[] ids, float[] style, float speed = 1.0f)
    {
        var tokens = new DenseTensor<long>(ids, [1, ids.Length]);
        var styleTensor = new DenseTensor<float>(style, [1, style.Length]);
        // Spelled out rather than a collection expression: [speed] binds to the dimensions
        // overload and fails as "cannot convert float to int", which reads like a different bug.
        var speedTensor = new DenseTensor<float>(new Memory<float>(new[] { speed }), new[] { 1 });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", tokens),
            NamedOnnxValue.CreateFromTensor("style", styleTensor),
            NamedOnnxValue.CreateFromTensor("speed", speedTensor),
        };

        using var results = session.Run(inputs);
        return [.. results.First().AsEnumerable<float>()];
    }

    private static int Say(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("say <ipa> <voice> <out.wav>");
            return 2;
        }

        var ids = Encode(args[1], out var unknown);

        if (unknown.Count > 0)
        {
            Console.Error.WriteLine($"not in the vocabulary: {string.Join(" ", unknown)}");
        }

        using var session = new InferenceSession(ModelPath());

        var audio = Synthesise(session, ids, Style(args[2], ids.Length));

        WriteWav(args[3], audio);

        Console.WriteLine($"{ids.Length} tokens -> {audio.Length:N0} samples " +
                          $"({audio.Length / (double)SampleRate:F2}s at {SampleRate} Hz) -> {args[3]}");

        return 0;
    }

    /// <summary>
    /// The question this spike exists for: is a blend a weighted average of two style vectors, and
    /// does the ONNX path take one without a wrapper in the middle?
    /// </summary>
    private static int Blend(string[] args)
    {
        if (args.Length < 6)
        {
            Console.Error.WriteLine("blend <ipa> <voiceA> <voiceB> <t> <out.wav>");
            return 2;
        }

        var ids = Encode(args[1], out _);
        var t = float.Parse(args[4]);

        var a = Style(args[2], ids.Length);
        var b = Style(args[3], ids.Length);
        var mixed = new float[a.Length];

        for (var i = 0; i < a.Length; i++)
        {
            mixed[i] = ((1 - t) * a[i]) + (t * b[i]);
        }

        using var session = new InferenceSession(ModelPath());

        var audio = Synthesise(session, ids, mixed);
        WriteWav(args[5], audio);

        // How far apart the two voices are at all, so "the blend sounds like a third voice" can be
        // told from "the blend sounds like whichever one dominates".
        var distance = Math.Sqrt(a.Zip(b, (x, y) => (x - y) * (x - y)).Sum());
        var fromA = Math.Sqrt(a.Zip(mixed, (x, y) => (x - y) * (x - y)).Sum());

        Console.WriteLine($"{args[2]} -> {args[3]} at t={t}");
        Console.WriteLine($"  style distance A..B : {distance:F3}");
        Console.WriteLine($"  blend distance A..M : {fromA:F3}");
        Console.WriteLine($"  {audio.Length / (double)SampleRate:F2}s -> {args[5]}");

        return 0;
    }

    private static int Bench(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("bench <ipa> <voice> [runs]");
            return 2;
        }

        var runs = args.Length > 3 && int.TryParse(args[3], out var n) ? n : 5;
        var ids = Encode(args[1], out _);
        var style = Style(args[2], ids.Length);

        // <b>Every published build, because five of the eight were never measured</b> (#139). The
        // three that were are the reason the rest have to be: fp32 is the fastest and the smallest
        // build was four times slower than the largest, so a picker that ranked them by file size
        // would tell a Commander almost nothing true about what they were choosing.
        //
        // <b>fp32 leads, and that is load-bearing rather than tidy</b>: it is the reference the
        // quality column below is measured against.
        foreach (var (label, file) in Builds)
        {
            var path = Path.Combine(_root, "onnx", file);

            if (!File.Exists(path))
            {
                continue;
            }

            var loaded = Stopwatch.StartNew();
            using var session = new InferenceSession(path);
            loaded.Stop();

            // One warm run first: the first inference pays for arena allocation and would
            // otherwise be reported as the cost of speaking.
            var audio = Synthesise(session, ids, style);

            var times = new List<double>();

            for (var i = 0; i < runs; i++)
            {
                var run = Stopwatch.StartNew();
                Synthesise(session, ids, style);
                run.Stop();
                times.Add(run.Elapsed.TotalMilliseconds);
            }

            times.Sort();

            var seconds = audio.Length / (double)SampleRate;
            var median = times[times.Count / 2];

            Console.WriteLine($"{label,-6} " +
                              $"load {loaded.ElapsedMilliseconds,5} ms   " +
                              $"synth median {median,7:F1} ms   " +
                              $"min {times[0],7:F1}   max {times[^1],7:F1}   " +
                              $"audio {seconds,5:F2}s   " +
                              $"realtime x{seconds * 1000 / median:F1}   " +
                              $"({new FileInfo(path).Length / 1024 / 1024} MB)");
        }

        return 0;
    }

    /// <summary>
    /// How far each build's audio departs from fp32's, for the same line and voice (#139).
    /// <para>
    /// <b>Quality is the third axis and nobody had looked at it.</b> The spike measured latency and
    /// listened to blends; it never compared how a quantised build <em>sounds</em> against fp32. A
    /// build that is fast and small and audibly worse is not a bargain, and the issue asks for that
    /// to be decided deliberately rather than by omission.
    /// </para>
    /// <para>
    /// <b>What this measures and what it does not.</b> The model is deterministic, so the same
    /// tokens and the same voice through a different build should produce the same waveform bar
    /// quantisation error — which makes fp32 a reference signal and the difference an error signal.
    /// The number is that ratio in dB, and it is a <em>proxy</em>: it says how far the samples moved,
    /// not whether a person minds. A build that scores badly here is worth listening to before it is
    /// offered or refused, which is why every one of them is also written out as a WAV.
    /// </para>
    /// <para>
    /// A length that does not match fp32's is reported rather than scored. It would mean the build
    /// produced different audio rather than the same audio less exactly, and averaging over a
    /// misalignment would report a bad number for a good reason.
    /// </para>
    /// </summary>
    private static int Quality(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("quality <ipa> <voice> [outdir]");
            return 2;
        }

        var ids = Encode(args[1], out _);
        var style = Style(args[2], ids.Length);
        var into = args.Length > 3 ? args[3] : Path.Combine(_root, "out", "quality");

        Directory.CreateDirectory(into);

        float[]? reference = null;

        foreach (var (label, file) in Builds)
        {
            var path = Path.Combine(_root, "onnx", file);

            if (!File.Exists(path))
            {
                Console.WriteLine($"{label,-10} not present");
                continue;
            }

            using var session = new InferenceSession(path);
            var audio = Synthesise(session, ids, style);

            WriteWav(Path.Combine(into, $"{label}.wav"), audio);

            if (reference is null)
            {
                reference = audio;
                Console.WriteLine($"{label,-10} reference, {audio.Length} samples");
                continue;
            }

            if (audio.Length != reference.Length)
            {
                Console.WriteLine(
                    $"{label,-10} {audio.Length} samples against the reference's {reference.Length} "
                    + "— different audio rather than the same audio less exactly");
                continue;
            }

            double signal = 0;
            double noise = 0;

            for (var i = 0; i < reference.Length; i++)
            {
                signal += (double)reference[i] * reference[i];
                var error = (double)audio[i] - reference[i];
                noise += error * error;
            }

            var snr = noise <= 0 ? double.PositiveInfinity : 10 * Math.Log10(signal / noise);

            Console.WriteLine(
                $"{label,-10} against fp32: {snr,6:F1} dB   " +
                $"({new FileInfo(path).Length / 1024 / 1024} MB)");
        }

        Console.WriteLine();
        Console.WriteLine($"WAVs in {into} — the number is a proxy; the ear is the test.");

        return 0;
    }

    /// <summary>16-bit mono PCM, so the result can be listened to rather than only measured.</summary>
    private static void WriteWav(string path, float[] samples)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        var bytes = samples.Length * 2;

        writer.Write("RIFF"u8);
        writer.Write(36 + bytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(bytes);

        foreach (var sample in samples)
        {
            writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }
    }
}
