namespace D47.Core.Conversation;

/// <summary>
/// An <see cref="ILlmProvider"/> that answers a <see cref="LlmRequest.Background"/> request with an
/// <see cref="LlmStreamEvent.Failed.Away"/> failure while <c>stopped</c> is true and the provider is not on this
/// machine, and forwards everything else unchanged.
/// </summary>
public class AwayGate : ILlmProvider
{
    private readonly ILlmProvider _inner;
    private readonly Func<bool> _stopped;

    private AwayGate(ILlmProvider inner, Func<bool> stopped)
    {
        _inner = inner;
        _stopped = stopped;
    }

    public string Id => _inner.Id;

    public bool RunsOnThisMachine => _inner.RunsOnThisMachine;

    public string DefaultModel => _inner.DefaultModel;

    /// <summary>Wraps <paramref name="inner"/>; the result is an <see cref="IModelLoader"/> exactly when it is.</summary>
    public static AwayGate Wrap(ILlmProvider inner, Func<bool> stopped)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(stopped);

        return inner is IModelLoader loader ? new Loading(inner, loader, stopped) : new AwayGate(inner, stopped);
    }

    public LlmProviderCapabilities CapabilitiesFor(string model) => _inner.CapabilitiesFor(model);

    public IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Background && !_inner.RunsOnThisMachine && _stopped()
            ? Refused()
            : _inner.StreamAsync(request, cancellationToken);
    }

    private static async IAsyncEnumerable<LlmStreamEvent> Refused()
    {
        await Task.CompletedTask.ConfigureAwait(false);

        yield return new LlmStreamEvent.Failed("not sent: the Commander is away", Transient: true) { Away = true };
    }

    private sealed class Loading(ILlmProvider inner, IModelLoader loader, Func<bool> stopped) : AwayGate(inner, stopped), IModelLoader
    {
        public Task<LoadedContext?> LoadedContextAsync(string model, CancellationToken cancellationToken) =>
            loader.LoadedContextAsync(model, cancellationToken);

        public Task<ContextReload> ReloadAsync(string model, int tokens, CancellationToken cancellationToken) =>
            loader.ReloadAsync(model, tokens, cancellationToken);
    }
}
