using System.Text;
using D47.App.Panel;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Knowledge;

public sealed class TheRareIndexIsDownloadedOnlyWhenStaleTests
{
    private const string Folder = @"C:\d47-test\data\rares";

    private static readonly string Copy = Path.Combine(Folder, RareIndexStore.FileName);

    private readonly MemoryFileSystem _files = new();

    private readonly List<int> _fetches = [];

    private DateTimeOffset _now;

    private byte[]? _served = IndexText("FreshRare");

    private static byte[] IndexText(string symbol) => Encoding.UTF8.GetBytes(
        "# built 2026-11-03T09:00:00Z\n# far prices 2026-11-03T09:00:00Z\n" + RareIndex.Header + "\n"
        + symbol + "\t17000\t5\t9000\t80\t2026-11-03T08:00:00Z\n");

    private RareIndexStore Store() => new(
        Folder,
        _files,
        () => _now,
        NullLogger.Instance,
        _ =>
        {
            _fetches.Add(_fetches.Count);
            return Task.FromResult(_served);
        });

    /// <summary>Writes the copy on disk and sets the clock <paramref name="age"/> after its write time.</summary>
    private void HeldFor(TimeSpan age)
    {
        _files.WriteBytes(Copy, IndexText("OldRare"));
        _now = new DateTimeOffset(_files.Stat(Copy)!.Value.Written, TimeSpan.Zero) + age;
    }

    private static string Only(RareIndex? index) => Assert.Single(Assert.IsType<RareIndex>(index).Rows).Symbol;

    [Fact]
    public async Task ACopyUnderThreeHoursOldIsNotReplaced()
    {
        HeldFor(TimeSpan.FromMinutes(179));

        var index = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_fetches);
        Assert.Equal("OldRare", Only(index));
    }

    [Fact]
    public async Task ACopyOverThreeHoursOldIsReplaced()
    {
        HeldFor(TimeSpan.FromMinutes(181));

        var index = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Single(_fetches);
        Assert.Equal("FreshRare", Only(index));
        Assert.Equal(_served, _files.ReadBytes(Copy));
    }

    [Fact]
    public async Task AFailedDownloadIsNotTriedAgainWithinTheHour()
    {
        HeldFor(TimeSpan.FromHours(4));
        _served = null;
        var store = Store();

        var first = await store.GetAsync(TestContext.Current.CancellationToken);
        _now += TimeSpan.FromMinutes(59);
        await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("OldRare", Only(first));
        Assert.Single(_fetches);

        _now += TimeSpan.FromMinutes(2);
        await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _fetches.Count);
    }

    [Fact]
    public async Task ADownloadThatDoesNotParseLeavesTheOldCopyInUse()
    {
        HeldFor(TimeSpan.FromHours(4));
        var before = _files.ReadBytes(Copy);
        _served = Encoding.UTF8.GetBytes("<html>Not found</html>");

        var index = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Single(_fetches);
        Assert.Equal("OldRare", Only(index));
        Assert.Equal(before, _files.ReadBytes(Copy));
    }

    [Fact]
    public async Task WithNoCopyAndNoDownloadThereIsNoIndex()
    {
        _now = DateTimeOffset.UnixEpoch;
        _served = null;

        Assert.Null(await Store().GetAsync(TestContext.Current.CancellationToken));
        Assert.Null(_files.Stat(Copy));
    }
}
