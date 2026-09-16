using D47.Donations.R2;
using D47.Donations.Store;
using Xunit;

namespace D47.Donations.Tests;

/// <summary>
/// An unreachable store and an empty one arrive at the same call and must never reach the same
/// decision: the local copies are deleted only against a listing that actually answered.
/// </summary>
public class AFailedListingDeletesNothingTests : IDisposable
{
    private const string Donor = "0123456789abcdef0123456789abcdef";
    private const string GoneKey = "excerpts/" + Donor + "/20260101T000000Z-aabbccddeeff0011.md.gz";
    private const string HeldKey = "excerpts/" + Donor + "/20260914T100926Z-1122334455667788.md.gz";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-donations-tests", Guid.NewGuid().ToString("n"));

    private readonly DownloadFolder _downloads;
    private readonly UtilityState _state = new();
    private readonly string _stateFile;

    public AFailedListingDeletesNothingTests()
    {
        _downloads = new DownloadFolder(_folder);
        _stateFile = Path.Combine(_folder, "state.json");

        Directory.CreateDirectory(_folder);

        foreach (var (key, zip) in new[] { (GoneKey, "gone.zip"), (HeldKey, "held.zip") })
        {
            File.WriteAllText(Path.Combine(_folder, zip), "a downloaded donation");
            _state.Downloaded[key] = zip;
        }
    }

    [Fact]
    public async Task AListingThatThrewDeletesNothingAndReportsTheFailure()
    {
        var inbox = new DonationInbox(_downloads, _state, _stateFile);

        var outcome = await inbox.RefreshAsync(
            _ => throw new R2Exception(System.Net.HttpStatusCode.Forbidden, "AccessDenied", "no"),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("AccessDenied", outcome.Problem!, StringComparison.Ordinal);
        Assert.Empty(outcome.Deleted);

        Assert.True(File.Exists(Path.Combine(_folder, "gone.zip")));
        Assert.True(File.Exists(Path.Combine(_folder, "held.zip")));
        Assert.Equal(2, _state.Downloaded.Count);
    }

    [Fact]
    public async Task AListingThatAnsweredDeletesTheCopiesTheStoreNoLongerHolds()
    {
        var inbox = new DonationInbox(_downloads, _state, _stateFile);

        var outcome = await inbox.RefreshAsync(
            _ => Task.FromResult<IReadOnlyList<StoredObject>>([Held()]),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal(new[] { "gone.zip" }, outcome.Deleted);

        Assert.False(File.Exists(Path.Combine(_folder, "gone.zip")));
        Assert.True(File.Exists(Path.Combine(_folder, "held.zip")));
        Assert.Equal(new[] { HeldKey }, _state.Downloaded.Keys);
    }

    [Fact]
    public async Task TheFirstOpeningCountsNoArrivals()
    {
        var inbox = new DonationInbox(_downloads, _state, _stateFile);

        var outcome = await inbox.RefreshAsync(
            _ => Task.FromResult<IReadOnlyList<StoredObject>>([Held()]),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Null(outcome.Arrived);
    }

    [Fact]
    public async Task RefreshingTwiceCountsAgainstTheSameOpeningBothTimes()
    {
        _state.LastOpened = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        var inbox = new DonationInbox(_downloads, _state, _stateFile);
        var opened = DateTimeOffset.UtcNow;

        Task<RefreshOutcome> Refresh() => inbox.RefreshAsync(
            _ => Task.FromResult<IReadOnlyList<StoredObject>>([Held()]),
            opened,
            CancellationToken.None);

        var first = await Refresh();
        var second = await Refresh();

        Assert.Equal(1, first.Arrived);
        Assert.Equal(first.Arrived, second.Arrived);
    }

    private static StoredObject Held()
    {
        Assert.True(DonationKey.TryParse(HeldKey, out var key));

        return new StoredObject(key, 1024, new DateTimeOffset(2026, 9, 14, 10, 9, 26, TimeSpan.Zero));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }
}
