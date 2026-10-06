namespace D47.Core.Knowledge;

/// <summary>The last best cargo search, so the spoken answer and the drawn one are one answer (#849).</summary>
public sealed class BestCargoBoard
{
    private readonly Lock _gate = new();

    private BestCargoPosting? _last;

    /// <summary>What was asked and what came back, or null if nothing has been asked yet.</summary>
    public BestCargoPosting? Last
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    /// <summary>Raised after each post, so a surface can redraw without polling.</summary>
    public event Action? Posted;

    public void Post(BestCargoPosting posting)
    {
        lock (_gate)
        {
            _last = posting;
        }

        Posted?.Invoke();
    }
}

/// <param name="Answer">What came back; null when the docked market's prices are not known.</param>
public sealed record BestCargoPosting(BestCargoSearch Search, BestCargoAnswer? Answer, DateTimeOffset AskedAt);
