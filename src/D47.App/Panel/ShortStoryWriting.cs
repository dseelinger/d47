using D47.Core.Adventures;

namespace D47.App.Panel;

/// <summary>
/// The short story being written, held for whichever Short Story page is open: its progress, its stop, and what became of
/// it until a page shows that. One write at a time. <see cref="Changed"/> is raised when a write begins, when its aside
/// changes and when it is settled, on whichever thread did it.
/// </summary>
public sealed class ShortStoryWriting(TimeProvider time)
{
    private readonly object _gate = new();
    private StoryWrite? _write;

    public ShortStoryWriting()
        : this(TimeProvider.System)
    {
    }

    public event Action? Changed;

    /// <summary>The write running, or settled and waiting for a page to show it, or null.</summary>
    public StoryWrite? Current
    {
        get
        {
            lock (_gate)
            {
                return _write;
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="draft"/> on the pool with the ask given a fresh <see cref="StoryProgress"/> and the write's token.
    /// Each line it passes to its aside callback is kept and given to <paramref name="say"/>; its outcome, unless stopped,
    /// is given to <paramref name="finished"/> once, which is expected to <see cref="Settle"/> it. False while another write is held.
    /// </summary>
    public bool Begin(
        AdventureAsk ask,
        string? drawnFor,
        Func<AdventureAsk, Action<string>, CancellationToken, Task<AdventureOutcome>> draft,
        Action<string> say,
        Action<StoryWrite> finished)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(say);
        ArgumentNullException.ThrowIfNull(finished);

        StoryWrite write;

        lock (_gate)
        {
            if (_write is not null)
            {
                return false;
            }

            write = new StoryWrite(ask with { Progress = new StoryProgress(time) }, drawnFor);
            _write = write;
        }

        void Aside(string line)
        {
            if (write.Token.IsCancellationRequested)
            {
                return;
            }

            write.Aside = line;
            say(line);
            Changed?.Invoke();
        }

        Changed?.Invoke();

        _ = Task.Run(async () =>
        {
            AdventureOutcome outcome;

            try
            {
                outcome = await draft(write.Ask, Aside, write.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (write.Token.IsCancellationRequested)
            {
                return;
            }

            lock (_gate)
            {
                if (!ReferenceEquals(_write, write) || write.Token.IsCancellationRequested)
                {
                    return;
                }

                write.Outcome = outcome;
            }

            finished(write);
        });

        return true;
    }

    /// <summary>Records what became of <paramref name="write"/> for a page to show; false when it is no longer the write held.</summary>
    public bool Settle(StoryWrite write, StoryOffer offer)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(offer);

        lock (_gate)
        {
            if (!ReferenceEquals(_write, write))
            {
                return false;
            }

            write.Offer = offer;
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Cancels the write under way; what it would have produced is never settled.</summary>
    public void Stop()
    {
        StoryWrite? write;

        lock (_gate)
        {
            write = _write;

            if (write is null || write.Offer is not null)
            {
                return;
            }

            _write = null;
        }

        write.Cancel();
    }

    /// <summary>The settled write, once: null while it runs, when there is none, or after another page took it.</summary>
    public StoryWrite? Take()
    {
        lock (_gate)
        {
            if (_write is not { Offer: not null } write)
            {
                return null;
            }

            _write = null;
            return write;
        }
    }
}

/// <summary>What a settled write left for the page: the draft stored under <see cref="Key"/>, or a <see cref="Note"/> saying why there is none.</summary>
public sealed record StoryOffer(string? Key, string? Name, string Note, bool Failed);

/// <summary>One short story write: what was asked, for whom, how far it has got, and what came back.</summary>
public sealed class StoryWrite(AdventureAsk ask, string? drawnFor)
{
    private readonly CancellationTokenSource _stop = new();
    private volatile AdventureOutcome? _outcome;
    private volatile StoryOffer? _offer;
    private volatile string? _aside;

    public AdventureAsk Ask { get; } = ask;

    /// <summary>The Commander shown when the write began.</summary>
    public string? DrawnFor { get; } = drawnFor;

    public StoryProgress Progress => Ask.Progress!;

    /// <summary>The last line said before a later draft, or null.</summary>
    public string? Aside
    {
        get => _aside;
        internal set => _aside = value;
    }

    public AdventureOutcome? Outcome
    {
        get => _outcome;
        internal set => _outcome = value;
    }

    public StoryOffer? Offer
    {
        get => _offer;
        internal set => _offer = value;
    }

    internal CancellationToken Token => _stop.Token;

    internal void Cancel() => _stop.Cancel();
}
