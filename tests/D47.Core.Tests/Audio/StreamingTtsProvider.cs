using D47.Core.Audio;

namespace D47.Core.Tests.Audio;

/// <summary>A provider whose clips arrive when the test appends to them.</summary>
public sealed class StreamingTtsProvider : ITtsProvider
{
    private readonly Dictionary<string, ArrivingClip> _clips = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource> _accepts = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public string Id => "streaming";

    public string Name => "Streaming";

    /// <summary>When set, a request is accepted only once the test calls <see cref="Accept"/>.</summary>
    public bool AcceptGated { get; init; }

    /// <summary>A voice this provider refuses before accepting the request.</summary>
    public string? Refuses { get; init; }

    public List<string?> Voices { get; } = [];

    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(VoiceCatalogue.Of([]));

    public async Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default) =>
        await (await StreamAsync(text, voice, cancellationToken).ConfigureAwait(false)).Whole.ConfigureAwait(false);

    public async Task<ArrivingClip> StreamAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        TaskCompletionSource? accept = null;

        lock (_lock)
        {
            Voices.Add(voice.VoiceId);

            if (AcceptGated)
            {
                accept = _accepts[text] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (accept is not null)
        {
            await accept.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Refuses is { } unwanted && string.Equals(voice.VoiceId, unwanted, StringComparison.Ordinal))
        {
            throw new TtsException($"an invalid ID has been received: '{unwanted}'", fault: TtsFault.VoiceRejected);
        }

        var clip = new ArrivingClip(text);
        cancellationToken.Register(() => clip.Fail(new OperationCanceledException(cancellationToken)));

        lock (_lock)
        {
            _clips[text] = clip;
        }

        return clip;
    }

    public void Accept(string text)
    {
        lock (_lock)
        {
            _accepts[text].TrySetResult();
        }
    }

    /// <summary>The clip returned for <paramref name="text"/>, once the request has been accepted.</summary>
    public async Task<ArrivingClip> ClipFor(string text)
    {
        for (var attempt = 0; attempt < 400; attempt++)
        {
            lock (_lock)
            {
                if (_clips.TryGetValue(text, out var clip))
                {
                    return clip;
                }
            }

            await Task.Delay(5).ConfigureAwait(false);
        }

        throw new TimeoutException($"\"{text}\" was never streamed");
    }

    public async Task WaitForRequest(string text)
    {
        for (var attempt = 0; attempt < 400; attempt++)
        {
            lock (_lock)
            {
                if (_accepts.ContainsKey(text))
                {
                    return;
                }
            }

            await Task.Delay(5).ConfigureAwait(false);
        }

        throw new TimeoutException($"\"{text}\" was never requested");
    }

    /// <summary>Polls until <paramref name="condition"/> holds.</summary>
    public static async Task Eventually(Func<bool> condition, string what)
    {
        for (var attempt = 0; attempt < 400; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(5).ConfigureAwait(false);
        }

        throw new TimeoutException(what);
    }
}
