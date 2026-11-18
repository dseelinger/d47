using D47.Core.Speech;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using static D47.Tts.ChatterboxPipeline;

namespace D47.Tts;

/// <summary>
/// Chatterbox Multilingual's four graphs, on the CPU, as the model card's script runs them: the whole line generated
/// and then decoded once.
/// </summary>
internal sealed class ChatterboxMultilingualPipeline : IChatterboxEngine
{
    private const long StartSpeechToken = 6561;
    private const long StopSpeechToken = 6562;
    private const float Exaggeration = 0.5f;
    private const float RepetitionPenalty = 1.2f;

    /// <summary>The most speech tokens any line may run to, 40 seconds.</summary>
    public const int MostTokens = 1000;

    private readonly InferenceSession _embed;
    private readonly InferenceSession _encoder;
    private readonly InferenceSession _language;
    private readonly InferenceSession _decoder;
    private readonly RunOptions _run = new();

    private ChatterboxMultilingualPipeline(
        InferenceSession embed, InferenceSession encoder, InferenceSession language, InferenceSession decoder)
    {
        _embed = embed;
        _encoder = encoder;
        _language = language;
        _decoder = decoder;
    }

    /// <summary>The most speech tokens a line of <paramref name="textTokens"/> ids may run to before it is cut off.</summary>
    public static int TokenLimit(int textTokens) => Math.Min(MostTokens, 60 + (8 * textTokens));

    /// <summary>
    /// The last row of the logits with each token already generated penalised once, however often it was generated, as
    /// the model card's <c>RepetitionPenaltyLogitsProcessor</c> does; then argmax.
    /// </summary>
    public static long Next(float[] logits, int vocabulary, IReadOnlyCollection<long> generated)
    {
        ArgumentNullException.ThrowIfNull(logits);
        ArgumentNullException.ThrowIfNull(generated);

        var row = logits.AsSpan(logits.Length - vocabulary, vocabulary).ToArray();

        foreach (var token in generated.Distinct())
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

    public static ChatterboxMultilingualPipeline Open(string folder, int threads)
    {
        var sessions = new List<InferenceSession>();

        try
        {
            foreach (var graph in (string[])["embed_tokens", "speech_encoder", "language_model", "conditional_decoder"])
            {
                using var options = new SessionOptions { IntraOpNumThreads = threads };
                sessions.Add(new InferenceSession(ChatterboxMultilingualAssets.GraphPath(folder, graph), options));
            }

            return new ChatterboxMultilingualPipeline(sessions[0], sessions[1], sessions[2], sessions[3]);
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

    public float[] Speak(long[] textIds, IDisposable voice, CancellationToken cancellationToken)
    {
        var encoded = (Voice)voice;
        var spoken = Generate(textIds, encoded.Conditioning, TokenLimit(textIds.Length), cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return spoken.Count == 0 ? [] : Decode(encoded, spoken);
    }

    /// <summary>
    /// Greedy decoding with a repetition penalty. The first step's text positions count from -1 and the special tokens
    /// sit at 0; each later token is at its step number. The language model takes no positions.
    /// </summary>
    private List<long> Generate(long[] textIds, OrtValue conditioning, int limit, CancellationToken cancellationToken)
    {
        var embedsType = TypeOf(_language.InputMetadata, "inputs_embeds");
        var exaggerationType = TypeOf(_embed.InputMetadata, "exaggeration");
        var pastNames = _language.InputNames.Where(n => n.Contains("past_key_values", StringComparison.Ordinal)).ToArray();
        var inputNames = new[] { "inputs_embeds", "attention_mask" }.Concat(pastNames).ToArray();
        var outputNames = new[] { "logits" }.Concat(PresentNames(pastNames)).ToArray();

        var past = pastNames.Select(EmptyCache).ToArray();
        var generated = new List<long> { StartSpeechToken };
        var spoken = new List<long>();
        var ids = textIds;
        var positions = new long[textIds.Length];
        var sequence = 0;

        for (var i = 0; i < textIds.Length; i++)
        {
            positions[i] = textIds[i] >= StartSpeechToken ? 0 : i - 1;
        }

        using var exaggeration = Make([Exaggeration], [1], exaggerationType);

        try
        {
            for (var step = 0; step < limit; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var inputIds = OrtValue.CreateTensorValueFromMemory(ids, [1, ids.Length]);
                using var positionIds = OrtValue.CreateTensorValueFromMemory(positions, [1, positions.Length]);

                var embedded = _embed.Run(
                    _run,
                    ["input_ids", "position_ids", "exaggeration"],
                    [inputIds, positionIds, exaggeration],
                    _embed.OutputNames);

                var embeds = embedded[0];

                if (step == 0)
                {
                    var joined = Concatenate(conditioning, embeds, embedsType);
                    embeds.Dispose();
                    embeds = joined;
                }

                sequence += (int)embeds.GetTensorTypeAndShape().Shape[1];

                using var attention = OrtValue.CreateTensorValueFromMemory(Ones(sequence), [1, sequence]);

                var inputs = new OrtValue[2 + past.Length];
                inputs[0] = embeds;
                inputs[1] = attention;
                past.CopyTo(inputs, 2);

                var results = _language.Run(_run, inputNames, inputs, outputNames);

                var vocabulary = (int)results[0].GetTensorTypeAndShape().Shape[^1];
                var next = Next(ReadFloats(results[0]), vocabulary, generated);

                results[0].Dispose();
                embeds.Dispose();

                foreach (var value in past)
                {
                    value.Dispose();
                }

                past = [.. results.Skip(1)];

                if (next == StopSpeechToken)
                {
                    break;
                }

                generated.Add(next);
                spoken.Add(next);

                ids = [next];
                positions = [step + 1];
            }
        }
        finally
        {
            foreach (var value in past)
            {
                value.Dispose();
            }
        }

        return spoken;

        OrtValue EmptyCache(string name)
        {
            var dimensions = _language.InputMetadata[name].Dimensions;
            var heads = dimensions[1] > 0 ? dimensions[1] : 16;
            var width = dimensions[3] > 0 ? dimensions[3] : 64;

            return OrtValue.CreateAllocatedTensorValue(
                OrtAllocator.DefaultInstance, TypeOf(_language.InputMetadata, name), [1, heads, 0, width]);
        }
    }

    /// <summary>The output for each past input: its <c>present</c> name where the graph uses it, otherwise the outputs after the logits in order.</summary>
    private IEnumerable<string> PresentNames(string[] pastNames)
    {
        var named = pastNames.Select(n => n.Replace("past_key_values", "present", StringComparison.Ordinal)).ToArray();

        return named.All(_language.OutputNames.Contains)
            ? named
            : _language.OutputNames.Where(n => n != "logits");
    }

    /// <summary>The voice's prompt tokens and then the line's, decoded in one run; the decoder leaves out the prompt's audio.</summary>
    private float[] Decode(Voice voice, List<long> spoken)
    {
        var tokens = new long[voice.PromptTokens.Length + spoken.Count];

        voice.PromptTokens.CopyTo(tokens, 0);
        spoken.CopyTo(tokens, voice.PromptTokens.Length);

        using var speech = OrtValue.CreateTensorValueFromMemory(tokens, [1, tokens.Length]);
        using var decoded = _decoder.Run(
            _run,
            ["speech_tokens", "speaker_embeddings", "speaker_features"],
            [speech, voice.SpeakerEmbeddings, voice.SpeakerFeatures],
            _decoder.OutputNames);

        return ReadFloats(decoded[0]);
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
