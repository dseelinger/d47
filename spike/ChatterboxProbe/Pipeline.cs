using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ChatterboxProbe;

/// <summary>Where each stage's time went, and what it produced.</summary>
/// <remarks>
/// <b><see cref="TotalMs"/> is the first-sound latency</b>, and that is the finding rather than an
/// approximation. Chatterbox's decoder is not incremental: it takes the whole speech-token sequence
/// and returns the whole waveform, so nothing can be heard until the last token has been generated.
/// There is no earlier moment to measure.
/// </remarks>
internal sealed record Timing(
    double LoadMs, double EncodeMs, double LanguageMs, double DecodeMs, int Tokens, int Samples)
{
    public double TotalMs => EncodeMs + LanguageMs + DecodeMs;

    public double Seconds => Samples / (double)Pipeline.SampleRate;

    public double Realtime => Seconds * 1000 / TotalMs;

    public double MsPerToken => Tokens == 0 ? 0 : LanguageMs / Tokens;
}

/// <summary>
/// Chatterbox's four ONNX graphs, driven from C#. The order and the token ids are Resemble's own,
/// from the usage sample on <c>ResembleAI/chatterbox-turbo-ONNX</c>; everything that sample gets
/// from numpy, librosa and <c>transformers</c> is written out here, because that substitution is
/// exactly what #293 asks whether d47 can make.
/// </summary>
internal sealed class Pipeline : IDisposable
{
    public const int SampleRate = 24000;

    private const long StartSpeechToken = 6561;
    private const long StopSpeechToken = 6562;
    private const long SilenceToken = 4299;
    private const int SilencePadding = 3;

    private readonly InferenceSession _embed;
    private readonly InferenceSession _encoder;
    private readonly InferenceSession _language;
    private readonly InferenceSession _decoder;
    private readonly RunOptions _run = new();

    public IReadOnlyList<(string Graph, string File, long Bytes)> Graphs { get; }

    public double LoadMs { get; }

    private Pipeline(
        InferenceSession embed,
        InferenceSession encoder,
        InferenceSession language,
        InferenceSession decoder,
        IReadOnlyList<(string, string, long)> graphs,
        double loadMs)
    {
        _embed = embed;
        _encoder = encoder;
        _language = language;
        _decoder = decoder;
        Graphs = graphs;
        LoadMs = loadMs;
    }

    public InferenceSession Embed => _embed;

    public InferenceSession Encoder => _encoder;

    public InferenceSession Language => _language;

    public InferenceSession Decoder => _decoder;

    /// <param name="onCpu">
    /// Graphs to keep on the CPU whatever <paramref name="provider"/> says. Not a debugging knob:
    /// the speech encoder runs once per <em>voice</em> rather than once per line, so pinning it is
    /// what a real integration would do anyway, and DirectML cannot run it at all (see the finding).
    /// </param>
    public static Pipeline Open(
        string root, string dtype, string provider, IReadOnlySet<string>? onCpu = null)
    {
        if (provider is not ("cpu" or "dml"))
        {
            throw new ArgumentException($"unknown provider '{provider}' — cpu or dml.");
        }

        var cpu = new SessionOptions();
        var accelerated = cpu;

        if (provider == "dml")
        {
            accelerated = new SessionOptions();

            // Both of these are DirectML's documented requirements, not tuning: the provider does
            // not support ORT's memory pattern planner, and it must run the graph sequentially.
            accelerated.EnableMemoryPattern = false;
            accelerated.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
            accelerated.AppendExecutionProvider_DML(0);
        }

        var chosen = new List<(string, string, long)>();
        var clock = Stopwatch.StartNew();

        var embed = Session("embed_tokens");
        var encoder = Session("speech_encoder");
        var language = Session("language_model");
        var decoder = Session("conditional_decoder");

        clock.Stop();

        return new Pipeline(embed, encoder, language, decoder, chosen, clock.Elapsed.TotalMilliseconds);

        InferenceSession Session(string graph)
        {
            var path = Resolve(root, graph, dtype);
            var bytes = new FileInfo(path).Length +
                        (File.Exists(path + "_data") ? new FileInfo(path + "_data").Length : 0);

            var options = onCpu?.Contains(graph) == true ? cpu : accelerated;

            chosen.Add((graph, $"{Path.GetFileName(path)} on {(options == cpu ? "cpu" : provider)}", bytes));

            return new InferenceSession(path, options);
        }
    }

    /// <summary>
    /// The requested dtype if the repository published it, and otherwise the one build of that graph
    /// that is there.
    /// <para>
    /// The fallback is not laziness: the Nano conversion publishes a <em>mixed</em> set — an fp16
    /// embedding, two q4f16 graphs and a q4 decoder — so "the q4f16 build" is not a thing that
    /// exists for all four, and a probe that insisted on one name could not open it at all.
    /// </para>
    /// </summary>
    private static string Resolve(string root, string graph, string dtype)
    {
        var suffix = dtype switch
        {
            "fp32" => string.Empty,
            "q8" => "_quantized",
            _ => $"_{dtype}",
        };

        var onnx = Path.Combine(root, "onnx");
        var exact = Path.Combine(onnx, $"{graph}{suffix}.onnx");

        if (File.Exists(exact))
        {
            return exact;
        }

        var present = Directory.Exists(onnx)
            ? Directory.GetFiles(onnx, $"{graph}*.onnx")
            : [];

        return present.Length switch
        {
            1 => present[0],
            0 => throw new FileNotFoundException($"no build of {graph} under {onnx}"),
            _ => throw new FileNotFoundException(
                $"{graph} has no {dtype} build; present: " +
                string.Join(", ", present.Select(Path.GetFileName))),
        };
    }

    /// <summary>One line, end to end: reference clip and text in, waveform out.</summary>
    public (float[] Audio, Timing Timing) Speak(
        long[] textIds, float[] reference, int maxTokens, float repetitionPenalty)
    {
        var encode = Stopwatch.StartNew();

        using var audioValues = Tensors.Make(
            reference, [1, reference.Length], TypeOf(_encoder.InputMetadata, "audio_values"));

        var encoded = _encoder.Run(
            _run, [_encoder.InputNames[0]], [audioValues], _encoder.OutputNames);

        var conditioning = encoded[0];
        var promptTokens = Tensors.ReadLongs(encoded[1]);
        var speakerEmbeddings = encoded[2];
        var speakerFeatures = encoded[3];

        encode.Stop();

        var language = Stopwatch.StartNew();
        var generated = Generate(textIds, conditioning, maxTokens, repetitionPenalty);
        language.Stop();

        var decode = Stopwatch.StartNew();
        var audio = Decode(promptTokens, generated, speakerEmbeddings, speakerFeatures);
        decode.Stop();

        foreach (var value in encoded)
        {
            value.Dispose();
        }

        return (audio, new Timing(
            LoadMs,
            encode.Elapsed.TotalMilliseconds,
            language.Elapsed.TotalMilliseconds,
            decode.Elapsed.TotalMilliseconds,
            generated.Length,
            audio.Length));
    }

    /// <summary>
    /// The autoregressive half. Greedy with a repetition penalty of 1.2, which is what Resemble's
    /// own sample does — sampling would make the probe's latency numbers unrepeatable and its WAVs
    /// unattributable to a change.
    /// </summary>
    private long[] Generate(
        long[] textIds, OrtValue conditioning, int maxTokens, float repetitionPenalty)
    {
        var embedsType = TypeOf(_language.InputMetadata, "inputs_embeds");
        var pastNames = _language.InputNames.Where(n => n.Contains("past_key_values")).ToArray();
        var presentNames = pastNames.Select(Present).ToArray();
        var inputNames = new[] { "inputs_embeds", "attention_mask", "position_ids" }
            .Concat(pastNames).ToArray();
        var outputNames = new[] { "logits" }.Concat(presentNames).ToArray();

        var past = pastNames.Select(EmptyCache).ToArray();
        var generated = new List<long> { StartSpeechToken };
        var ids = textIds;
        var sequence = 0;

        for (var step = 0; step < maxTokens; step++)
        {
            using var inputIds = Tensors.Make(ids, [1, ids.Length]);

            var embedded = _embed.Run(_run, [_embed.InputNames[0]], [inputIds], _embed.OutputNames);
            var embeds = embedded[0];

            if (step == 0)
            {
                var joined = Tensors.Concatenate(conditioning, embeds, embedsType);
                embeds.Dispose();
                embeds = joined;
            }

            var length = (int)embeds.GetTensorTypeAndShape().Shape[1];
            sequence += length;

            using var attention = Tensors.Make(Ones(sequence), [1, sequence]);
            using var positions = Tensors.Make(Positions(sequence, length), [1, length]);

            var inputs = new OrtValue[3 + past.Length];
            inputs[0] = embeds;
            inputs[1] = attention;
            inputs[2] = positions;
            past.CopyTo(inputs, 3);

            var results = _language.Run(_run, inputNames, inputs, outputNames);

            var vocabulary = (int)results[0].GetTensorTypeAndShape().Shape[^1];
            var next = Argmax(Tensors.ReadFloats(results[0]), vocabulary, generated, repetitionPenalty);

            results[0].Dispose();
            embeds.Dispose();

            foreach (var value in past)
            {
                value.Dispose();
            }

            past = [.. results.Skip(1)];

            generated.Add(next);

            if (next == StopSpeechToken)
            {
                break;
            }

            ids = [next];
        }

        foreach (var value in past)
        {
            value.Dispose();
        }

        return [.. generated];

        static string Present(string past) => past.Replace("past_key_values", "present");

        OrtValue EmptyCache(string name)
        {
            var dimensions = _language.InputMetadata[name].Dimensions;
            var heads = dimensions[1] > 0 ? dimensions[1] : 16;
            var width = dimensions[3] > 0 ? dimensions[3] : 64;

            return OrtValue.CreateAllocatedTensorValue(
                OrtAllocator.DefaultInstance,
                TypeOf(_language.InputMetadata, name),
                [1, heads, 0, width]);
        }
    }

    private float[] Decode(
        long[] promptTokens, long[] generated, OrtValue speakerEmbeddings, OrtValue speakerFeatures)
    {
        // Resemble's sample drops the leading start token and the trailing stop token, then pads
        // with three silence tokens so the vocoder does not clip the last word.
        var spoken = generated[1..(generated.Length - 1)];
        var tokens = new long[promptTokens.Length + spoken.Length + SilencePadding];

        promptTokens.CopyTo(tokens, 0);
        spoken.CopyTo(tokens, promptTokens.Length);
        Array.Fill(tokens, SilenceToken, tokens.Length - SilencePadding, SilencePadding);

        using var speech = Tensors.Make(tokens, [1, tokens.Length]);
        using var embeddings = Tensors.Cast(
            speakerEmbeddings, TypeOf(_decoder.InputMetadata, "speaker_embeddings"));
        using var features = Tensors.Cast(
            speakerFeatures, TypeOf(_decoder.InputMetadata, "speaker_features"));

        var decoded = _decoder.Run(
            _run,
            ["speech_tokens", "speaker_embeddings", "speaker_features"],
            [speech, embeddings, features],
            _decoder.OutputNames);

        var audio = Tensors.ReadFloats(decoded[0]);

        foreach (var value in decoded)
        {
            value.Dispose();
        }

        return audio;
    }

    /// <summary>The last row of the logits, penalised for what has already been said, then argmax.</summary>
    private static long Argmax(float[] logits, int vocabulary, List<long> generated, float penalty)
    {
        var row = logits.AsSpan(logits.Length - vocabulary, vocabulary).ToArray();

        foreach (var token in generated)
        {
            var score = row[token];
            row[token] = score < 0 ? score * penalty : score / penalty;
        }

        var best = 0;

        for (var i = 1; i < row.Length; i++)
        {
            if (row[i] > row[best])
            {
                best = i;
            }
        }

        return best;
    }

    private static long[] Ones(int length)
    {
        var ones = new long[length];
        Array.Fill(ones, 1L);
        return ones;
    }

    private static long[] Positions(int sequence, int length)
    {
        var positions = new long[length];

        for (var i = 0; i < length; i++)
        {
            positions[i] = sequence - length + i;
        }

        return positions;
    }

    private static TensorElementType TypeOf(
        IReadOnlyDictionary<string, NodeMetadata> metadata, string name) =>
        metadata[name].ElementDataType;

    public void Dispose()
    {
        _run.Dispose();
        _decoder.Dispose();
        _language.Dispose();
        _encoder.Dispose();
        _embed.Dispose();
    }
}
