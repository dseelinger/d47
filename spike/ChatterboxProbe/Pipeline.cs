using System.Diagnostics;
using System.Runtime.InteropServices;
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

/// <summary>The CPU thread-pool knobs, per graph, and the diagnostics that go with them.</summary>
/// <remarks>
/// <para>
/// All four were defaulted until #182's finding was recalled: transcription's three-second floor
/// was a thread-count problem, on this same hybrid-core machine. One count for all four graphs was
/// the first cut, and it is a compromise between two opposite shapes — the language model is 70
/// sequential runs of almost no work each, which is what punishes a wide pool and a spinning one,
/// and the decoder is one large pass that wants every core. A graph's count falls back to
/// <see cref="Threads"/>, and 0 is ONNX Runtime's own default.
/// </para>
/// <para>
/// <see cref="GlobalPool"/> is the other way to stop four pools competing for the same cores: one
/// environment-wide pool of <see cref="Threads"/> that every session shares, instead of one each.
/// </para>
/// </remarks>
internal sealed record Tuning(
    int Threads = 0,
    int EncoderThreads = 0,
    int LanguageThreads = 0,
    int DecoderThreads = 0,
    bool? Spin = null,
    bool GlobalPool = false,
    string? ProfilePrefix = null,
    bool Verbose = false);

/// <summary>
/// What the speech encoder makes of a reference clip: the four values every line in that voice
/// needs, owned here so that no line pays for them.
/// </summary>
internal sealed record Voice(
    OrtValue Conditioning,
    long[] PromptTokens,
    OrtValue SpeakerEmbeddings,
    OrtValue SpeakerFeatures,
    double EncodeMs,
    IReadOnlyList<IDisposable> Owned) : IDisposable
{
    public void Dispose()
    {
        foreach (var owned in Owned)
        {
            owned.Dispose();
        }
    }
}

/// <summary>One decoded piece of a streamed line, and what it cost.</summary>
internal sealed record Chunk(int Tokens, double LanguageMs, double DecodeMs, int Samples)
{
    public double AudioMs => Samples * 1000.0 / Pipeline.SampleRate;
}

/// <summary>Where a streamed line's time went, and whether playback would have kept up.</summary>
internal sealed record StreamTiming(IReadOnlyList<Chunk> Chunks, double TotalMs)
{
    public double FirstSoundMs => Chunks.Count == 0 ? 0 : Chunks[0].LanguageMs + Chunks[0].DecodeMs;

    public int Samples => Chunks.Sum(c => c.Samples);

    public double Seconds => Samples / (double)Pipeline.SampleRate;

    /// <summary>
    /// Time the listener would spend waiting mid-line if each piece were played as soon as it
    /// existed and everything ran on one thread: a piece is ready when its language and decode
    /// work is done, and due when the piece before it has finished playing.
    /// </summary>
    public double StallMs => Stall(pipelined: false);

    /// <summary>The same with the decoder on a thread of its own, running behind the language model.</summary>
    public double PipelinedStallMs => Stall(pipelined: true);

    private double Stall(bool pipelined)
    {
        var language = 0.0;
        var ready = 0.0;
        var playing = 0.0;
        var stall = 0.0;

        foreach (var chunk in Chunks)
        {
            language += chunk.LanguageMs;
            ready = pipelined
                ? Math.Max(language, ready) + chunk.DecodeMs
                : ready + chunk.LanguageMs + chunk.DecodeMs;

            var start = Math.Max(ready, playing);

            if (playing > 0)
            {
                stall += start - playing;
            }

            playing = start + chunk.AudioMs;
        }

        return stall;
    }
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

    /// <summary>Speech tokens come at 25 a second, so each is 40 ms of audio. Checked at run time.</summary>
    public const int SamplesPerToken = SampleRate / 25;

    private const long StartSpeechToken = 6561;
    private const long StopSpeechToken = 6562;
    private const long SilenceToken = 4299;
    private const int SilencePadding = 3;

    private readonly InferenceSession _embed;
    private readonly InferenceSession _encoder;
    private readonly InferenceSession _language;
    private readonly InferenceSession _decoder;
    private readonly RunOptions _run = new();
    private readonly bool _profiling;

    public IReadOnlyList<(string Graph, string File, long Bytes)> Graphs { get; }

    public double LoadMs { get; }

    private Pipeline(
        InferenceSession embed,
        InferenceSession encoder,
        InferenceSession language,
        InferenceSession decoder,
        IReadOnlyList<(string, string, long)> graphs,
        double loadMs,
        bool profiling)
    {
        _embed = embed;
        _encoder = encoder;
        _language = language;
        _decoder = decoder;
        Graphs = graphs;
        LoadMs = loadMs;
        _profiling = profiling;
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
        string root,
        string dtype,
        string provider,
        IReadOnlySet<string>? onCpu = null,
        Tuning? tuning = null,
        IReadOnlyDictionary<string, string>? epOptions = null)
    {
        if (provider is not ("cpu" or "dml" or "webgpu"))
        {
            throw new ArgumentException($"unknown provider '{provider}' — cpu, dml or webgpu.");
        }

        tuning ??= new Tuning();
        epOptions ??= new Dictionary<string, string>();

        if (tuning.GlobalPool || tuning.Verbose)
        {
            // Before the first session, because the environment is a singleton: the shared pool is
            // a property of it, and so is the log level a provider's own messages go through — a
            // session's level does not reach them.
            var creation = new EnvironmentCreationOptions
            {
                logLevel = tuning.Verbose
                    ? OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE
                    : OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING,
            };

            if (tuning.GlobalPool)
            {
                creation.threadOptions = new OrtThreadingOptions
                {
                    GlobalIntraOpNumThreads = tuning.Threads,
                    GlobalInterOpNumThreads = 1,
                    GlobalSpinControl = tuning.Spin ?? true,
                };
            }

            OrtEnv.CreateInstanceWithOptions(ref creation);
        }

        SessionOptions? accelerated = null;

        if (provider == "dml")
        {
            accelerated = new SessionOptions();

            // Both of these are DirectML's documented requirements, not tuning: the provider does
            // not support ORT's memory pattern planner, and it must run the graph sequentially.
            accelerated.EnableMemoryPattern = false;
            accelerated.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
            accelerated.AppendExecutionProvider_DML(0);
        }
        else if (provider == "webgpu")
        {
            accelerated = WebGpu(epOptions, tuning.Verbose);
        }

        var chosen = new List<(string, string, long)>();
        var clock = Stopwatch.StartNew();

        // The embedding lookup runs inside the language model's loop, so it takes that graph's count.
        var embed = Session("embed_tokens", tuning.LanguageThreads);
        var encoder = Session("speech_encoder", tuning.EncoderThreads);
        var language = Session("language_model", tuning.LanguageThreads);
        var decoder = Session("conditional_decoder", tuning.DecoderThreads);

        clock.Stop();

        return new Pipeline(
            embed, encoder, language, decoder, chosen, clock.Elapsed.TotalMilliseconds,
            tuning.ProfilePrefix is not null);

        InferenceSession Session(string graph, int threads)
        {
            var path = Resolve(root, graph, dtype);
            var bytes = new FileInfo(path).Length +
                        (File.Exists(path + "_data") ? new FileInfo(path + "_data").Length : 0);

            var pinned = accelerated is null || onCpu?.Contains(graph) == true;
            var options = pinned ? Cpu(graph, threads) : accelerated!;

            chosen.Add((graph, $"{Path.GetFileName(path)} on {(pinned ? "cpu" : provider)}", bytes));

            return new InferenceSession(path, options);
        }

        SessionOptions Cpu(string graph, int threads)
        {
            var options = new SessionOptions();

            if (tuning.GlobalPool)
            {
                options.DisablePerSessionThreads();
            }
            else
            {
                if (threads == 0)
                {
                    threads = tuning.Threads;
                }

                if (threads > 0)
                {
                    options.IntraOpNumThreads = threads;
                }

                if (tuning.Spin is not null)
                {
                    options.AddSessionConfigEntry(
                        "session.intra_op.allow_spinning", tuning.Spin.Value ? "1" : "0");
                }
            }

            if (tuning.ProfilePrefix is not null)
            {
                // The prefix first: the property that turns profiling on reads it at that moment.
                options.ProfileOutputPathPrefix = $"{tuning.ProfilePrefix}-{graph}";
                options.EnableProfiling = true;
            }

            if (tuning.Verbose)
            {
                options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE;
            }

            return options;
        }
    }

    /// <summary>
    /// The WebGPU plugin provider: registered once per process, then the one device it reports.
    /// Only the <c>webgpu</c> build carries the package, so the other two say so rather than
    /// failing inside ONNX Runtime.
    /// </summary>
    private static SessionOptions WebGpu(IReadOnlyDictionary<string, string> epOptions, bool verbose)
    {
#if WEBGPU
        var env = OrtEnv.Instance();

        if (!_webGpuRegistered)
        {
            env.RegisterExecutionProviderLibrary(
                "webgpu_ep", Microsoft.ML.OnnxRuntime.EP.WebGpu.WebGpuEp.GetLibraryPath());
            _webGpuRegistered = true;
        }

        var device = env.GetEpDevices().FirstOrDefault(
            d => d.EpName == Microsoft.ML.OnnxRuntime.EP.WebGpu.WebGpuEp.GetEpName())
            ?? throw new InvalidOperationException("the WebGPU provider found no device.");

        var options = new SessionOptions();
        options.AppendExecutionProvider(env, [device], epOptions);

        if (verbose)
        {
            options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_VERBOSE;
        }

        return options;
#else
        throw new NotSupportedException("build with -p:Ep=webgpu for --provider webgpu.");
#endif
    }

#if WEBGPU
    private static bool _webGpuRegistered;
#endif

    /// <summary>Ends profiling on every session and returns the JSON files ONNX Runtime wrote.</summary>
    public IReadOnlyList<string> EndProfiling() => _profiling
        ? [_embed.EndProfiling(), _encoder.EndProfiling(), _language.EndProfiling(), _decoder.EndProfiling()]
        : [];

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

    /// <summary>
    /// One line, end to end: reference clip and text in, waveform out. The speech tokens come out
    /// too, because they are the only way to compare two providers — the decoder draws its own
    /// noise, so two runs of one token sequence never give the same samples.
    /// </summary>
    public (float[] Audio, long[] Tokens, Timing Timing) Speak(
        long[] textIds, float[] reference, int maxTokens, float repetitionPenalty)
    {
        using var voice = Encode(reference);

        var language = Stopwatch.StartNew();
        var generated = Generate(textIds, voice.Conditioning, maxTokens, repetitionPenalty).ToArray();
        language.Stop();

        var decode = Stopwatch.StartNew();
        var audio = Decode(voice, Spoken(generated), pad: true);
        decode.Stop();

        return (audio, generated, new Timing(
            LoadMs,
            voice.EncodeMs,
            language.Elapsed.TotalMilliseconds,
            decode.Elapsed.TotalMilliseconds,
            generated.Length,
            audio.Length));
    }

    /// <summary>
    /// The speech encoder's account of a reference clip — everything that is per voice rather than
    /// per line, which a real integration computes once and keeps.
    /// </summary>
    public Voice Encode(float[] reference)
    {
        var clock = Stopwatch.StartNew();

        using var audioValues = Tensors.Make(
            reference, [1, reference.Length], TypeOf(_encoder.InputMetadata, "audio_values"));

        var encoded = _encoder.Run(
            _run, [_encoder.InputNames[0]], [audioValues], _encoder.OutputNames);

        // Cast to what the decoder wants here, once per voice, rather than once per piece: Nano's
        // encoder emits fp16 speaker features and its decoder takes fp32.
        var embeddings = Tensors.Cast(encoded[2], TypeOf(_decoder.InputMetadata, "speaker_embeddings"));
        var features = Tensors.Cast(encoded[3], TypeOf(_decoder.InputMetadata, "speaker_features"));

        clock.Stop();

        return new Voice(
            encoded[0], Tensors.ReadLongs(encoded[1]), embeddings, features,
            clock.Elapsed.TotalMilliseconds, [encoded, embeddings, features]);
    }

    /// <summary>
    /// The same line, decoded in pieces as its tokens arrive — the only way first sound can come
    /// before the last token, since the decoder itself is one-shot. Each piece after the first is
    /// decoded with <paramref name="overlap"/> tokens of what came before it in front as context;
    /// the context's samples are thrown away bar a crossfade of <paramref name="crossfade"/>
    /// samples at the seam. Whether the seams can be heard is a separate question from the timing
    /// this returns, and the community README warns that they can.
    /// </summary>
    public (float[] Audio, long[] Tokens, StreamTiming Timing) Stream(
        long[] textIds,
        Voice voice,
        int maxTokens,
        float repetitionPenalty,
        int chunkTokens,
        int overlap,
        int crossfade)
    {
        var total = Stopwatch.StartNew();
        var generated = new List<long>();
        var spoken = new List<long>();
        var audio = new List<float>();
        var chunks = new List<Chunk>();
        var decodedTo = 0;
        var language = Stopwatch.StartNew();

        foreach (var token in Generate(textIds, voice.Conditioning, maxTokens, repetitionPenalty))
        {
            generated.Add(token);

            if (token == StartSpeechToken)
            {
                continue;
            }

            var last = token == StopSpeechToken;

            if (!last)
            {
                spoken.Add(token);
            }

            if (last || spoken.Count - decodedTo >= chunkTokens)
            {
                Flush(last);
            }

            if (last)
            {
                break;
            }
        }

        // The line ran into --max-tokens rather than a stop token.
        if (spoken.Count > decodedTo)
        {
            Flush(last: true);
        }

        total.Stop();

        return ([.. audio], [.. generated], new StreamTiming(chunks, total.Elapsed.TotalMilliseconds));

        void Flush(bool last)
        {
            if (spoken.Count == decodedTo)
            {
                return;
            }

            var languageMs = language.Elapsed.TotalMilliseconds;
            var context = Math.Min(overlap, decodedTo);
            var fresh = spoken.Count - decodedTo;

            var decode = Stopwatch.StartNew();
            var piece = Decode(voice, CollectionsMarshal.AsSpan(spoken)[(decodedTo - context)..], last);
            decode.Stop();

            var expected = (context + fresh + (last ? SilencePadding : 0)) * SamplesPerToken;

            if (piece.Length != expected)
            {
                throw new InvalidOperationException(
                    $"the decoder returned {piece.Length} samples for {context + fresh} tokens; " +
                    $"expected {expected} at {SamplesPerToken} a token.");
            }

            Append(audio, piece, context * SamplesPerToken, crossfade);

            chunks.Add(new Chunk(fresh, languageMs, decode.Elapsed.TotalMilliseconds, piece.Length - context * SamplesPerToken));

            decodedTo = spoken.Count;
            language.Restart();
        }
    }

    /// <summary>
    /// Joins a piece onto the audio so far. Its first <paramref name="skip"/> samples re-say what
    /// the previous piece already said and are dropped, except that the last <paramref name="fade"/>
    /// of them are blended over the previous piece's tail so the seam is a crossfade, not a cut.
    /// </summary>
    private static void Append(List<float> audio, float[] piece, int skip, int fade)
    {
        fade = Math.Min(fade, Math.Min(skip, audio.Count));

        var tail = CollectionsMarshal.AsSpan(audio)[^fade..];

        for (var i = 0; i < fade; i++)
        {
            var t = (i + 1) / (float)(fade + 1);
            tail[i] = tail[i] * (1 - t) + piece[skip - fade + i] * t;
        }

        audio.AddRange(piece.AsSpan(skip));
    }

    /// <summary>The tokens the decoder should say: no leading start, no trailing stop.</summary>
    private static ReadOnlySpan<long> Spoken(long[] generated)
    {
        var span = generated.AsSpan();

        if (span.Length > 0 && span[0] == StartSpeechToken)
        {
            span = span[1..];
        }

        if (span.Length > 0 && span[^1] == StopSpeechToken)
        {
            span = span[..^1];
        }

        return span;
    }

    /// <summary>
    /// The autoregressive half. Greedy with a repetition penalty of 1.2, which is what Resemble's
    /// own sample does — sampling would make the probe's latency numbers unrepeatable and its WAVs
    /// unattributable to a change.
    /// </summary>
    private IEnumerable<long> Generate(
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

        yield return StartSpeechToken;

        try
        {
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

                yield return next;

                if (next == StopSpeechToken)
                {
                    break;
                }

                ids = [next];
            }
        }
        finally
        {
            foreach (var value in past)
            {
                value.Dispose();
            }
        }

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

    /// <param name="pad">
    /// Whether these are the last tokens of the line. Resemble's sample pads the end with three
    /// silence tokens so the vocoder does not clip the last word; a piece with more to follow gets
    /// no such pad, or the silence would land mid-line.
    /// </param>
    private float[] Decode(Voice voice, ReadOnlySpan<long> spoken, bool pad)
    {
        var promptTokens = voice.PromptTokens;
        var padding = pad ? SilencePadding : 0;
        var tokens = new long[promptTokens.Length + spoken.Length + padding];

        promptTokens.CopyTo(tokens, 0);
        spoken.CopyTo(tokens.AsSpan(promptTokens.Length));
        Array.Fill(tokens, SilenceToken, tokens.Length - padding, padding);

        using var speech = Tensors.Make(tokens, [1, tokens.Length]);

        // The voice's, already in the decoder's types and owned by the voice — not disposed here,
        // because the next piece needs them.
        var embeddings = voice.SpeakerEmbeddings;
        var features = voice.SpeakerFeatures;

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
