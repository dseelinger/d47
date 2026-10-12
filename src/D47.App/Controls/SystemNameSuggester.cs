namespace D47.App.Controls;

/// <summary>
/// Asks for the names that start with what was typed, after a pause, and passes on only the answer for
/// the latest text. Call <see cref="Typed"/> and <see cref="Cancel"/> on one thread; <c>post</c> brings answers
/// back to it.
/// </summary>
public sealed class SystemNameSuggester<T>(
    Func<string, CancellationToken, Task<IReadOnlyList<T>>> lookup,
    Func<bool> enabled,
    Action<Action> post,
    TimeSpan pause)
{
    private CancellationTokenSource? _latest;

    /// <summary>Raised with the names for the latest text, or with none when there is nothing to show.</summary>
    public event Action<IReadOnlyList<T>>? Answered;

    /// <summary>
    /// Drops the request before it and asks for <paramref name="typed"/>; the task ends once its answer has been
    /// passed on or dropped.
    /// </summary>
    public Task Typed(string typed)
    {
        Cancel();

        typed = typed.Trim();

        if (typed.Length < NameSuggestions.MinimumLength || !enabled())
        {
            Answered?.Invoke([]);
            return Task.CompletedTask;
        }

        var request = _latest = new CancellationTokenSource();

        return Ask(typed, request);
    }

    /// <summary>Drops the pending request, so no answer arrives for it.</summary>
    public void Cancel()
    {
        _latest?.Cancel();
        _latest?.Dispose();
        _latest = null;
    }

    private async Task Ask(string typed, CancellationTokenSource request)
    {
        var token = request.Token;
        IReadOnlyList<T> matches;

        try
        {
            if (pause > TimeSpan.Zero)
            {
                await Task.Delay(pause, token).ConfigureAwait(false);
            }

            matches = enabled() ? await lookup(typed, token).ConfigureAwait(false) : [];
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            matches = [];
        }

        post(() =>
        {
            if (!ReferenceEquals(request, _latest))
            {
                return;
            }

            _latest = null;
            request.Dispose();
            Answered?.Invoke(matches);
        });
    }
}
