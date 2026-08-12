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

    public int Cancelled { get; private set; }

    public Task<IReadOnlyList<VoiceInfo>> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VoiceInfo>>([new VoiceInfo("fake-1", "Fake One", "en-GB")]);

    public async Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        TaskCompletionSource? gate = null;

        lock (_lock)
        {
            Requested.Add(text);

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
