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

    /// <summary>Raised inside <see cref="Typed"/> when the text qualifies and a request starts.</summary>
    public event Action? Asking;

    /// <summary>Raised with the lookup's answer for the latest text, empty when nothing matches.</summary>
    public event Action<IReadOnlyList<T>>? Answered;

    /// <summary>Raised in place of <see cref="Answered"/> when the lookup throws.</summary>
    public event Action? Failed;

    /// <summary>Raised when the text is under the minimum length or suggestions are off.</summary>
    public event Action? Cleared;

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
            Cleared?.Invoke();
            return Task.CompletedTask;
        }

        var request = _latest = new CancellationTokenSource();
        Asking?.Invoke();

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
        IReadOnlyList<T>? matches = null;
        var failed = false;

        try
        {
            if (pause > TimeSpan.Zero)
            {
                await Task.Delay(pause, token).ConfigureAwait(false);
            }

            if (enabled())
            {
                matches = await lookup(typed, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            failed = true;
        }

        post(() =>
        {
            if (!ReferenceEquals(request, _latest))
            {
                return;
            }

            _latest = null;
            request.Dispose();

            if (failed)
            {
                Failed?.Invoke();
            }
            else if (matches is null)
            {
                Cleared?.Invoke();
            }
            else
            {
                Answered?.Invoke(matches);
            }
        });
    }
}
