using D47.Donations.R2;

namespace D47.Donations.Store;

/// <summary>
/// What one press of Refresh does: list, then prune what the store no longer holds.
/// </summary>
/// <remarks>
/// A failed listing deletes nothing. An empty bucket and an unreachable one are the same shape at the
/// call site and must never be the same decision here, which is why the prune sits inside the success
/// path rather than after it.
/// </remarks>
public sealed class DonationInbox(DownloadFolder downloads, UtilityState state, string stateFile)
{
    /// <summary>
    /// The previous opening, read once. Refreshing within a session must not move the mark it counts
    /// arrivals against.
    /// </summary>
    private readonly DateTimeOffset? _previousOpening = state.LastOpened;

    public async Task<RefreshOutcome> RefreshAsync(
        Func<CancellationToken, Task<IReadOnlyList<StoredObject>>> list,
        DateTimeOffset openedAt,
        CancellationToken cancel)
    {
        IReadOnlyList<StoredObject> objects;

        try
        {
            objects = await list(cancel);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RefreshOutcome.Failed(ex.Message);
        }

        var deleted = downloads.Prune(
            state,
            objects.Select(stored => stored.Key.Key).ToHashSet(StringComparer.Ordinal));

        state.LastOpened = openedAt;
        state.Write(stateFile);

        return new RefreshOutcome(
            true,
            objects,
            _previousOpening is null
                ? null
                : objects.Count(stored => stored.LastModified > _previousOpening),
            deleted,
            null);
    }
}

/// <summary>
/// The result of one listing. <see cref="Arrived"/> is null on the first opening, when there is no
/// previous one to count against.
/// </summary>
public sealed record RefreshOutcome(
    bool Succeeded,
    IReadOnlyList<StoredObject> Objects,
    int? Arrived,
    IReadOnlyList<string> Deleted,
    string? Problem)
{
    public static RefreshOutcome Failed(string problem) => new(false, [], null, [], problem);
}
