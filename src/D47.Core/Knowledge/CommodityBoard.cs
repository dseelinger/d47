namespace D47.Core.Knowledge;

/// <summary>
/// The last commodity answer, so the spoken one and the drawn one are one answer (Phase 49).
/// <para>
/// <b>The same arrangement <see cref="RoutePlanBook"/> makes for routes</b>, and for the same
/// reason: a Commander who asks by voice and then looks at the panel should see what they were
/// just told, not a second search that might disagree with it. The capability writes here on its
/// way out; the panel reads.
/// </para>
/// <para>
/// <b>In memory only, and that is deliberate.</b> A route plan is worth keeping across a restart;
/// a commodity price is the thing in d47 that ages fastest — supply can be stripped in hours — so
/// writing one to disk would build the exact trap the rest of this phase is careful about, an
/// answer that looks current because it was saved rather than because it is true.
/// </para>
/// </summary>
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

/// <param name="Query">What was asked.</param>
/// <param name="Answer">What came back.</param>
/// <param name="Near">The system it was measured from.</param>
/// <param name="AskedAt">
/// When. Shown on the page, because "this answer is itself twenty minutes old" is a different
/// caveat from "these prices were reported six hours ago" and a Commander needs both.
/// </param>
public sealed record CommodityPosting(
    CommodityQuery Query,
    CommodityAnswer Answer,
    string Near,
    DateTimeOffset AskedAt);
