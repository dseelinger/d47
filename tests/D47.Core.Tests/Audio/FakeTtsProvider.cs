using D47.Core.Audio;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Synthesis with the network taken out and the timing put under the test's control. Each
/// sentence's render can be released individually, which is how "the second sentence finished
/// first" — the ordering hazard the pipeline exists to prevent — becomes a test rather than a
/// race nobody can reproduce.
/// </summary>
public sealed class FakeTtsProvider : ITtsProvider
{
    private readonly Dictionary<string, TaskCompletionSource> _gates = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public string Id => "fake";

    public string Name => "Fake";

    public List<string> Requested { get; } = [];

    /// <summary>When set, synthesis waits for <see cref="Release"/> before returning.</summary>
    public bool Gated { get; init; }

    /// <summary>Sentences containing this throw, standing in for a provider that is down.</summary>
    public string? FailOn { get; init; }

    /// <summary>
    /// A voice this provider will not accept, standing in for one issued by a different
    /// provider or since deleted. Named voices other than this one are fine, and no voice at
    /// all is always fine — which is what makes the fallback observable.
    /// </summary>
    public string? Refuses { get; init; }

    /// <summary>Every voice it was asked for, in order, including the null after a refusal.</summary>
    public List<string?> Voices { get; } = [];

    public int Cancelled { get; private set; }

    /// <summary>
    /// What this provider reports when asked for its voices. Settable, so a test can stand in
    /// for a provider that answered and had none, or one that refused the key — which are
    /// different empties and are shown differently (Phase 19).
    /// </summary>
    public VoiceCatalogue Catalogue { get; init; } =
        VoiceCatalogue.Of([new VoiceInfo("fake-1", "Fake One", "en-GB")]);

    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Catalogue);

    public async Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        TaskCompletionSource? gate = null;

        lock (_lock)
        {
            Requested.Add(text);
            Voices.Add(voice.VoiceId);

            if (Gated)
            {
                gate = _gates[text] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (gate is not null)
        {
            using var registration = cancellationToken.Register(() =>
            {
                lock (_lock)
                {
                    Cancelled++;
                }

                gate.TrySetCanceled(cancellationToken);
            });

            await gate.Task.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (FailOn is { } marker && text.Contains(marker, StringComparison.Ordinal))
        {
            throw new TtsException($"the fake provider was told to fail on \"{marker}\"");
        }

        if (Refuses is { } unwanted && string.Equals(voice.VoiceId, unwanted, StringComparison.Ordinal))
        {
            throw new TtsException(
                $"an invalid ID has been received: '{unwanted}'",
                fault: TtsFault.VoiceRejected);
        }

        // Length proportional to the text, so a duration assertion means something.
        return new AudioClip(text, new byte[Math.Max(2, text.Length * 96)], AudioFormat.Standard);
    }

    /// <summary>Lets one sentence's synthesis finish. Order of release need not match request order.</summary>
    public void Release(string text)
    {
        TaskCompletionSource? gate;

        lock (_lock)
        {
            _gates.TryGetValue(text, out gate);
        }

        gate?.TrySetResult();
    }

    public void ReleaseAll()
    {
        List<TaskCompletionSource> gates;

        lock (_lock)
        {
            gates = [.. _gates.Values];
        }

        foreach (var gate in gates)
        {
            gate.TrySetResult();
        }
    }

    public async Task WaitForRequest(string text)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            lock (_lock)
            {
                if (Requested.Contains(text))
                {
                    return;
                }
            }

            await Task.Delay(5).ConfigureAwait(false);
        }

        throw new TimeoutException($"synthesis of \"{text}\" was never requested");
    }
}
