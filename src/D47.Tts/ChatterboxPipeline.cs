using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using D47.Core.Speech;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace D47.Tts;

/// <summary>What <see cref="ChatterboxTtsProvider"/> runs: a voice encoded once, then lines spoken in it.</summary>
internal interface IChatterboxEngine : IDisposable
{
    /// <summary>The speech encoder's output for a 24 kHz mono reference clip, owned by the caller.</summary>
    IDisposable Encode(float[] reference);

    /// <summary>One line of token ids, spoken in <paramref name="voice"/>, as 24 kHz mono samples.</summary>
    float[] Speak(long[] textIds, IDisposable voice, CancellationToken cancellationToken);
}

/// <summary>Chatterbox Turbo's four q4 graphs, on the CPU. The order and token ids are Resemble's own.</summary>
internal sealed class ChatterboxPipeline : IChatterboxEngine
{
    public const int SampleRate = 24_000;

    /// <summary>Speech tokens come at 25 a second.</summary>
    private const int SamplesPerToken = SampleRate / 25;

    private const long StartSpeechToken = 6561;
    private const long StopSpeechToken = 6562;
    private const long SilenceToken = 4299;
    private const int SilencePadding = 3;

    private const int MaxTokens = 1000;
    private const float RepetitionPenalty = 1.2f;
    private const int PieceTokens = 20;
    private const int ContextTokens = 3;

    /// <summary>10 ms blended at each seam.</summary>
    private const int Crossfade = 240;

    private readonly InferenceSession _embed;
    private readonly InferenceSession _encoder;
    private readonly InferenceSession _language;
    private readonly InferenceSession _decoder;
    private readonly RunOptions _run = new();

    private ChatterboxPipeline(
        InferenceSession embed, InferenceSession encoder, InferenceSession language, InferenceSession decoder)
    {
        _embed = embed;
        _encoder = encoder;
        _language = language;
        _decoder = decoder;
    }

    public static ChatterboxPipeline Open(string folder, int threads)
    {
        var sessions = new List<InferenceSession>();

        try
        {
            foreach (var graph in (string[])["embed_tokens", "speech_encoder", "language_model", "conditional_decoder"])
            {
                using var options = new SessionOptions { IntraOpNumThreads = threads };
                sessions.Add(new InferenceSession(ChatterboxAssets.GraphPath(folder, graph), options));
            }

            return new ChatterboxPipeline(sessions[0], sessions[1], sessions[2], sessions[3]);
        }
        catch
        {
            foreach (var session in sessions)
            {
                session.Dispose();
            }

            throw;
        }
    }

    public IDisposable Encode(float[] reference)
    {
        using var audioValues = Make(reference, [1, reference.Length], TypeOf(_encoder.InputMetadata, "audio_values"));

        var encoded = _encoder.Run(_run, [_encoder.InputNames[0]], [audioValues], _encoder.OutputNames);

        try
        {
            var embeddings = Cast(encoded[2], TypeOf(_decoder.InputMetadata, "speaker_embeddings"));
            var features = Cast(encoded[3], TypeOf(_decoder.InputMetadata, "speaker_features"));

            return new Voice(encoded[0], ReadLongs(encoded[1]), embeddings, features, [encoded, embeddings, features]);
        }
        catch
        {
            encoded.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The line decoded in pieces as its tokens arrive, the decoder on its own thread behind the language
    /// model. Each piece after the first carries <see cref="ContextTokens"/> of the one before as context,
    /// whose samples are dropped bar a crossfade at the seam.
    /// </summary>
    public float[] Speak(long[] textIds, IDisposable voice, CancellationToken cancellationToken)
    {
        var encoded = (Voice)voice;
        var spoken = new List<long>();
        var audio = new List<float>();
        var decodedTo = 0;

        using var queue = new BlockingCollection<Piece>(new ConcurrentQueue<Piece>());
        var worker = Task.Factory.StartNew(
            () =>
            {
                foreach (var piece in queue.GetConsumingEnumerable())
                {
                    Consume(piece);
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        try
        {
            foreach (var token in Generate(textIds, encoded.Conditioning, cancellationToken))
            {
                if (token == StartSpeechToken)
                {
                    continue;
                }

                var last = token == StopSpeechToken;

                if (!last)
                {
                    spoken.Add(token);
                }

                if (last || spoken.Count - decodedTo >= PieceTokens)
                {
                    Flush(last);
                }

                if (last)
                {
                    break;
                }
            }

            // Ran into MaxTokens rather than a stop token.
            Flush(last: true);
        }
        finally
        {
            queue.CompleteAdding();

            // GetAwaiter().GetResult() so a decoder that threw surfaces its own exception.
            worker.GetAwaiter().GetResult();
        }

        cancellationToken.ThrowIfCancellationRequested();

        return [.. audio];

        void Flush(bool last)
        {
            if (worker.IsFaulted)
            {
                worker.GetAwaiter().GetResult();
            }

            if (spoken.Count == decodedTo)
            {
                return;
            }

            var context = Math.Min(ContextTokens, decodedTo);
            var fresh = spoken.Count - decodedTo;

            queue.Add(new Piece([.. CollectionsMarshal.AsSpan(spoken)[(decodedTo - context)..]], context, fresh, last));
            decodedTo = spoken.Count;
        }

        void Consume(Piece work)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var piece = Decode(encoded, work.Tokens, work.Last);

            // The kept tail is anchored from the end: the decoder drops the voice's prompt itself, but
            // not always to the sample, so any difference lands in the discarded head.
            var keep = (work.Fresh + (work.Last ? SilencePadding : 0)) * SamplesPerToken;
            var skip = piece.Length - keep;

            if (skip < 0 || skip > (work.Context + 2) * SamplesPerToken)
            {
                throw new InvalidOperationException(
                    $"The Chatterbox decoder returned {piece.Length} samples for {work.Context} context and "
                    + $"{work.Fresh} fresh tokens.");
            }

            Append(audio, piece, skip, Crossfade);
        }
    }

    private sealed record Piece(long[] Tokens, int Context, int Fresh, bool Last);

    /// <summary>Joins a piece on, dropping its first <paramref name="skip"/> samples bar a crossfade over the previous tail.</summary>
    private static void Append(List<float> audio, float[] piece, int skip, int fade)
    {
        fade = Math.Min(fade, Math.Min(skip, audio.Count));

        var tail = CollectionsMarshal.AsSpan(audio)[^fade..];

        for (var i = 0; i < fade; i++)
        {
            var t = (i + 1) / (float)(fade + 1);
            tail[i] = (tail[i] * (1 - t)) + (piece[skip - fade + i] * t);
        }

        audio.AddRange(piece.AsSpan(skip));
    }

    /// <summary>Greedy decoding with a repetition penalty, as Resemble's sample does.</summary>
    private IEnumerable<long> Generate(long[] textIds, OrtValue conditioning, CancellationToken cancellationToken)
    {
        var embedsType = TypeOf(_language.InputMetadata, "inputs_embeds");
        var pastNames = _language.InputNames.Where(n => n.Contains("past_key_values", StringComparison.Ordinal)).ToArray();
        var presentNames = pastNames.Select(n => n.Replace("past_key_values", "present", StringComparison.Ordinal)).ToArray();
        var inputNames = new[] { "inputs_embeds", "attention_mask", "position_ids" }.Concat(pastNames).ToArray();
        var outputNames = new[] { "logits" }.Concat(presentNames).ToArray();

        var past = pastNames.Select(EmptyCache).ToArray();
        var generated = new List<long> { StartSpeechToken };
        var ids = textIds;
        var sequence = 0;

        yield return StartSpeechToken;

        try
        {
            for (var step = 0; step < MaxTokens; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var inputIds = OrtValue.CreateTensorValueFromMemory(ids, [1, ids.Length]);

                var embedded = _embed.Run(_run, [_embed.InputNames[0]], [inputIds], _embed.OutputNames);
                var embeds = embedded[0];

                if (step == 0)
                {
                    var joined = Concatenate(conditioning, embeds, embedsType);
                    embeds.Dispose();
                    embeds = joined;
                }

                var length = (int)embeds.GetTensorTypeAndShape().Shape[1];
                sequence += length;

                using var attention = OrtValue.CreateTensorValueFromMemory(Ones(sequence), [1, sequence]);
                using var positions = OrtValue.CreateTensorValueFromMemory(Positions(sequence, length), [1, length]);

                var inputs = new OrtValue[3 + past.Length];
                inputs[0] = embeds;
                inputs[1] = attention;
                inputs[2] = positions;
                past.CopyTo(inputs, 3);

                var results = _language.Run(_run, inputNames, inputs, outputNames);

                var vocabulary = (int)results[0].GetTensorTypeAndShape().Shape[^1];
                var next = Argmax(ReadFloats(results[0]), vocabulary, generated);

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

        OrtValue EmptyCache(string name)
        {
            var dimensions = _language.InputMetadata[name].Dimensions;
            var heads = dimensions[1] > 0 ? dimensions[1] : 16;
            var width = dimensions[3] > 0 ? dimensions[3] : 64;

            return OrtValue.CreateAllocatedTensorValue(
                OrtAllocator.DefaultInstance, TypeOf(_language.InputMetadata, name), [1, heads, 0, width]);
        }
    }

    /// <summary>The voice's prompt tokens, then these, then three silence tokens if this is the end of the line.</summary>
    private float[] Decode(Voice voice, long[] spoken, bool last)
    {
        var padding = last ? SilencePadding : 0;
        var tokens = new long[voice.PromptTokens.Length + spoken.Length + padding];

        voice.PromptTokens.CopyTo(tokens, 0);
        spoken.CopyTo(tokens, voice.PromptTokens.Length);
        Array.Fill(tokens, SilenceToken, tokens.Length - padding, padding);

        using var speech = OrtValue.CreateTensorValueFromMemory(tokens, [1, tokens.Length]);
        using var decoded = _decoder.Run(
            _run,
            ["speech_tokens", "speaker_embeddings", "speaker_features"],
            [speech, voice.SpeakerEmbeddings, voice.SpeakerFeatures],
            _decoder.OutputNames);

        return ReadFloats(decoded[0]);
    }

    /// <summary>The last row of the logits, penalised for what has been said, then argmax.</summary>
    private static long Argmax(float[] logits, int vocabulary, List<long> generated)
    {
        var row = logits.AsSpan(logits.Length - vocabulary, vocabulary).ToArray();

        foreach (var token in generated)
        {
            var score = row[token];
            row[token] = score < 0 ? score * RepetitionPenalty : score / RepetitionPenalty;
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

    private static TensorElementType TypeOf(IReadOnlyDictionary<string, NodeMetadata> metadata, string name) =>
        metadata[name].ElementDataType;

    private static OrtValue Make(float[] data, long[] shape, TensorElementType type)
    {
        if (type == TensorElementType.Float)
        {
            return OrtValue.CreateTensorValueFromMemory(data, shape);
        }

        if (type != TensorElementType.Float16)
        {
            throw new NotSupportedException($"{type} is not a float tensor type.");
        }

        var value = OrtValue.CreateAllocatedTensorValue(OrtAllocator.DefaultInstance, TensorElementType.Float16, shape);
        var span = value.GetTensorMutableDataAsSpan<Float16>();

        for (var i = 0; i < data.Length; i++)
        {
            span[i] = (Float16)data[i];
        }

        return value;
    }

    private static float[] ReadFloats(OrtValue value)
    {
        var type = value.GetTensorTypeAndShape().ElementDataType;

        if (type == TensorElementType.Float)
        {
            return value.GetTensorDataAsSpan<float>().ToArray();
        }

        if (type != TensorElementType.Float16)
        {
            throw new NotSupportedException($"{type} is not a float tensor type.");
        }

        var half = value.GetTensorDataAsSpan<Float16>();
        var floats = new float[half.Length];

        for (var i = 0; i < half.Length; i++)
        {
            floats[i] = (float)half[i];
        }

        return floats;
    }

    private static long[] ReadLongs(OrtValue value) => value.GetTensorDataAsSpan<long>().ToArray();

    /// <summary>The value itself when it is already <paramref name="type"/>, otherwise a converted copy.</summary>
    private static OrtValue Cast(OrtValue value, TensorElementType type) =>
        value.GetTensorTypeAndShape().ElementDataType == type
            ? value
            : Make(ReadFloats(value), value.GetTensorTypeAndShape().Shape, type);

    /// <summary>The reference clip's conditioning joined in front of the text embedding, along the sequence axis.</summary>
    private static OrtValue Concatenate(OrtValue first, OrtValue second, TensorElementType type)
    {
        var left = first.GetTensorTypeAndShape().Shape;
        var right = second.GetTensorTypeAndShape().Shape;

        if (left[2] != right[2])
        {
            throw new InvalidOperationException(
                $"The Chatterbox conditioning is {left[2]} wide and the text embedding is {right[2]}.");
        }

        var a = ReadFloats(first);
        var b = ReadFloats(second);
        var joined = new float[a.Length + b.Length];

        a.CopyTo(joined, 0);
        b.CopyTo(joined, a.Length);

        return Make(joined, [1, left[1] + right[1], left[2]], type);
    }

    public void Dispose()
    {
        _run.Dispose();
        _decoder.Dispose();
        _language.Dispose();
        _encoder.Dispose();
        _embed.Dispose();
    }

    /// <summary>Everything per voice rather than per line; disposing it releases the encoder's output.</summary>
    private sealed class Voice(
        OrtValue conditioning,
        long[] promptTokens,
        OrtValue speakerEmbeddings,
        OrtValue speakerFeatures,
        IReadOnlyList<IDisposable> owned) : IDisposable
    {
        public OrtValue Conditioning { get; } = conditioning;

        public long[] PromptTokens { get; } = promptTokens;

        public OrtValue SpeakerEmbeddings { get; } = speakerEmbeddings;

        public OrtValue SpeakerFeatures { get; } = speakerFeatures;

        public void Dispose()
        {
            foreach (var value in owned)
            {
                value.Dispose();
            }
        }
    }
}
