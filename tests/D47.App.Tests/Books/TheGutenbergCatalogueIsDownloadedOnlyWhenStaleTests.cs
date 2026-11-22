using System.IO.Compression;
using System.Text;
using D47.App.Panel;
using D47.Core.Books;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Books;

[Trait("Category", "Integration")]
public sealed class TheGutenbergCatalogueIsDownloadedOnlyWhenStaleTests
{
    private const string Folder = @"C:\d47-test\data\books\gutenberg";

    private static readonly string Copy = Path.Combine(Folder, GutenbergCatalogueStore.FileName);

    private readonly MemoryFileSystem _files = new();

    private int _fetches;

    private DateTimeOffset _now;

    private byte[]? _served = Gzip("Fresh Book");

    private static byte[] Gzip(string title)
    {
        using var buffer = new MemoryStream();

        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            var text = GutenbergCatalogue.Header + "\n" + $"1\t{title}\tAuthor\ten\tAdventure\t5\t2020-01-01\n";
            gzip.Write(Encoding.UTF8.GetBytes(text));
        }

        return buffer.ToArray();
    }

    private GutenbergCatalogueStore Store() => new(
        Folder,
        _files,
        () => _now,
        NullLogger.Instance,
        _ =>
        {
            _fetches++;
            return Task.FromResult(_served);
        });

    private void HeldFor(TimeSpan age)
    {
        _files.WriteBytes(Copy, Gzip("Old Book"));
        _now = new DateTimeOffset(_files.Stat(Copy)!.Value.Written, TimeSpan.Zero) + age;
    }

    private static string Only(GutenbergCatalogue? catalogue) =>
        Assert.Single(Assert.IsType<GutenbergCatalogue>(catalogue).Search(new GutenbergQuery()).Books).Title;

    [Fact]
    public async Task AMissingCopyIsFetchedAndStored()
    {
        _now = DateTimeOffset.UnixEpoch;

        var catalogue = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _fetches);
        Assert.Equal("Fresh Book", Only(catalogue));
        Assert.Equal(_served, _files.ReadBytes(Copy));
    }

    [Fact]
    public async Task ASecondCallWithinSevenDaysDoesNotFetch()
    {
        HeldFor(TimeSpan.FromDays(6));
        var store = Store();

        var first = await store.GetAsync(TestContext.Current.CancellationToken);
        var second = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, _fetches);
        Assert.Equal("Old Book", Only(first));
        Assert.Same(first, second);
    }

    [Fact]
    public async Task ACallAfterSevenDaysFetchesANewCopy()
    {
        HeldFor(TimeSpan.FromDays(8));

        var catalogue = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _fetches);
        Assert.Equal("Fresh Book", Only(catalogue));
    }

    [Fact]
    public async Task AFailedFetchKeepsTheOldCopy()
    {
        HeldFor(TimeSpan.FromDays(8));
        var before = _files.ReadBytes(Copy);
        _served = null;

        var catalogue = await Store().GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _fetches);
        Assert.Equal("Old Book", Only(catalogue));
        Assert.Equal(before, _files.ReadBytes(Copy));
    }

    [Fact]
    public async Task AFailedFetchIsNotTriedAgainWithinTheHour()
    {
        HeldFor(TimeSpan.FromDays(8));
        _served = null;
        var store = Store();

        await store.GetAsync(TestContext.Current.CancellationToken);
        _now += TimeSpan.FromMinutes(59);
        await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _fetches);

        _now += TimeSpan.FromMinutes(2);
        await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _fetches);
    }

    [Fact]
    public async Task WithNoCopyAndNoFetchThereIsNoCatalogue()
    {
        _now = DateTimeOffset.UnixEpoch;
        _served = null;

        Assert.Null(await Store().GetAsync(TestContext.Current.CancellationToken));
        Assert.Null(_files.Stat(Copy));
    }
}
