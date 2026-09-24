using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>What became of one utterance handed to a transcriber.</summary>
internal sealed record HearingOutcome
{
    /// <summary>What the transcriber returned, before the probe was consulted.</summary>
    public Transcription? Raw { get; init; }

    /// <summary>What to act on: <see cref="Raw"/>, or empty when the probe refused it.</summary>
    public Transcription? Kept { get; init; }

    /// <summary>The probe's no-speech reading, when it refused the transcript.</summary>
    public double? RefusedAt { get; init; }

    /// <summary>The sentence to say instead, when no words came back.</summary>
    public string? Problem { get; init; }
}

/// <summary>Turns one utterance into words, with the unprompted no-speech probe beside it (#196).</summary>
internal static class Hearing
{
    /// <summary>A probe reading at or above this refuses the transcript.</summary>
    public const double NoSpeechFloor = 0.6;

    /// <param name="probe">Started before the transcription and awaited after it.</param>
    public static async Task<HearingOutcome> TranscribeAsync(
        ISpeechTranscriber transcriber,
        SttProviderInfo provider,
        Func<Task<double?>> probe,
        Utterance utterance,
        IReadOnlyList<string> properNouns)
    {
        // Nothing is sent without a key.
        if (provider.Hosted && !transcriber.IsReady)
        {
            return new HearingOutcome { Problem = SttProviderCatalog.NoKey(provider) };
        }

        var noSpeech = probe();

        Transcription transcription;

        try
        {
            transcription = await transcriber.TranscribeAsync(utterance, properNouns).ConfigureAwait(false);
        }
        catch (TranscriptionUnavailableException failure)
        {
            return new HearingOutcome { Problem = SttProviderCatalog.Problem(failure) };
        }

        if (transcription.Text.Length > 0
            && await noSpeech.ConfigureAwait(false) is { } reading
            && reading >= NoSpeechFloor)
        {
            return new HearingOutcome
            {
                Raw = transcription,
                Kept = transcription with { Text = string.Empty },
                RefusedAt = reading,
            };
        }

        return new HearingOutcome { Raw = transcription, Kept = transcription };
    }

    /// <summary>The transcriber for a hosted provider, reading its key from <paramref name="key"/> at each call.</summary>
    public static OpenAiCompatibleTranscriber Hosted(
        SttProviderInfo provider,
        Func<string?> key,
        ILoggerFactory loggers,
        HttpMessageHandler? handler = null)
    {
        var endpoint = provider.Id switch
        {
            SttProviderCatalog.GroqId => OpenAiCompatibleTranscriber.GroqEndpoint,
            SttProviderCatalog.OpenAiId => OpenAiCompatibleTranscriber.OpenAiEndpoint,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider.Id, "Not a hosted provider."),
        };

        return new OpenAiCompatibleTranscriber(
            provider.Name,
            key,
            endpoint,
            provider.Model!,
            loggers.CreateLogger<OpenAiCompatibleTranscriber>(),
            handler);
    }
}
