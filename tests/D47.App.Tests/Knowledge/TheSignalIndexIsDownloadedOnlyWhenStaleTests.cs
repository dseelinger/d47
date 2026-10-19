using System.IO.Compression;
using System.Text;
using D47.App.Panel;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Knowledge;

public sealed class TheSignalIndexIsDownloadedOnlyWhenStaleTests
{
    private const string Folder = @"C:\d47-test\data\signals";

    private static readonly string Copy = Path.Combine(Folder, SignalIndexStore.FileName);

    private readonly MemoryFileSystem _files = new();

    private readonly List<int> _fetches = [];

    private DateTimeOffset _now;

    private byte[]? _served = Gzip("1001\tAlpha\t0\t0\t0\tInstallation\tFresh Depot\t2026-10-18");

    private static byte[] Gzip(params string[] rows)
    {
        using var buffer = new MemoryStream();

        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(string.Join("\n", [SignalIndex.Header, .. rows]) + "\n"));
        }

        return buffer.ToArray();
    }

    private SignalIndexStore Store(bool allowed = true) => new(
        Folder,
        _files,
        () => allowed,
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
        _files.WriteBytes(Copy, Gzip("1001\tAlpha\t0\t0\t0\tInstallation\tOld Depot\t2026-10-01"));
        _now = new DateTimeOffset(_files.Stat(Copy)!.Value.Written, TimeSpan.Zero) + age;
    }

    private static string Only(SignalIndex? index) => Assert.Single(Assert.IsType<SignalIndex>(index)).SignalName;

    [Fact]
    public async Task ACopyTwentyOneHoursOldIsReplaced()
    {
        HeldFor(TimeSpan.FromHours(21));

        var index = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Single(_fetches);
        Assert.Equal("Fresh Depot", Only(index));
        Assert.Equal(_served, _files.ReadBytes(Copy));
    }

    [Fact]
    public async Task ACopyTwoHoursOldIsNotReplaced()
    {
        HeldFor(TimeSpan.FromHours(2));

        var index = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_fetches);
        Assert.Equal("Old Depot", Only(index));
    }

    [Fact]
    public async Task WithTheSwitchOffNothingIsAskedForAndNoIndexIsGiven()
    {
        HeldFor(TimeSpan.FromHours(21));

        var index = await Store(allowed: false).GetAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_fetches);
        Assert.Null(index);
    }

    [Fact]
    public async Task AFailedDownloadKeepsTheOldCopy()
    {
        HeldFor(TimeSpan.FromHours(21));
        var before = _files.ReadBytes(Copy);
        _served = null;

        var index = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Single(_fetches);
        Assert.Equal("Old Depot", Only(index));
        Assert.Equal(before, _files.ReadBytes(Copy));
    }

    [Fact]
    public async Task ADownloadThatIsNotAnIndexKeepsTheOldCopy()
    {
        HeldFor(TimeSpan.FromHours(21));
        var before = _files.ReadBytes(Copy);
        _served = Encoding.UTF8.GetBytes("<html>Not found</html>");

        var index = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Old Depot", Only(index));
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

    [Fact]
    public async Task AFailedDownloadIsNotTriedAgainWithinTheHour()
    {
        HeldFor(TimeSpan.FromHours(21));
        _served = null;
        var store = Store();

        await store.GetAsync(TestContext.Current.CancellationToken);
        _now += TimeSpan.FromMinutes(59);
        await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Single(_fetches);

        _now += TimeSpan.FromMinutes(2);
        await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _fetches.Count);
    }

    [Fact]
    public async Task TheParsedIndexIsKeptUntilTheNextDownload()
    {
        HeldFor(TimeSpan.FromHours(2));
        var store = Store();

        var first = await store.GetAsync(TestContext.Current.CancellationToken);
        var second = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }
}
