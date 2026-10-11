using D47.Core.Knowledge;

namespace D47.App.Controls;

/// <summary>
/// Asks for the system names that start with what was typed, after a pause, and passes on only the answer for
/// the latest text. Call <see cref="Typed"/> and <see cref="Cancel"/> on one thread; <c>post</c> brings answers
/// back to it.
/// </summary>
public sealed class SystemNameSuggester(
    Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>> lookup,
    Func<bool> enabled,
    Action<Action> post,
    TimeSpan pause)
{
    /// <summary>The fewest characters that are sent.</summary>
    public const int MinimumLength = 3;

    /// <summary>How long typing has to stop before the names are asked for.</summary>
    public static readonly TimeSpan DefaultPause = TimeSpan.FromMilliseconds(300);

    private CancellationTokenSource? _latest;

    /// <summary>Raised with the names for the latest text, or with none when there is nothing to show.</summary>
    public event Action<IReadOnlyList<SystemNameMatch>>? Answered;

    /// <summary>
    /// Drops the request before it and asks for <paramref name="typed"/>; the task ends once its answer has been
    /// passed on or dropped.
    /// </summary>
    public Task Typed(string typed)
    {
        Cancel();

        typed = typed.Trim();

        if (typed.Length < MinimumLength || !enabled())
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
        IReadOnlyList<SystemNameMatch> matches;

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
