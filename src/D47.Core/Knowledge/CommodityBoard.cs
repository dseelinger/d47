namespace D47.Core.Knowledge;

/// <summary>The last commodity answer, so the spoken one and the drawn one are one answer (Phase 49).</summary>
public sealed class CommodityBoard
{
    private readonly Lock _gate = new();

    private CommodityPosting? _last;

    /// <summary>What was asked and what came back, or null if nothing has been asked yet.</summary>
    public CommodityPosting? Last
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    public void Post(CommodityPosting posting)
    {
        lock (_gate)
        {
            _last = posting;
        }
    }

    /// <summary>Raised when a new answer lands, so a surface can redraw without polling.</summary>
    public event Action? Posted;

    public void Announce() => Posted?.Invoke();
}

/// <summary>
/// <param name="Query">What was asked.</param> <param name="Answer">What came back.</param> <param
/// name="Near">The system it was measured from.</param> <param name="AskedAt"> When.
/// </summary>
/// <param name="Query">What was asked.</param>
/// <param name="Answer">What came back.</param>
/// <param name="Near">The system it was measured from.</param>
/// <param name="AskedAt">When.</param>
public sealed record CommodityPosting(
    CommodityQuery Query,
    CommodityAnswer Answer,
    string Near,
    DateTimeOffset AskedAt);
