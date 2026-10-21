using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheGalNetArchiveIsFetchedAndKeptTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private const string ArchivePath = @"C:\d47\data\galnet.json";

    private static GalNetArticle Article(string id, DateTimeOffset published, string title = "Title") =>
        new(id, title, ["One.", "Two."], published, "20 OCT 3312");

    private sealed class Source(params GalNetPage?[] pages) : IGalNetSource
    {
        public List<int> Offsets { get; } = [];

        public Task<GalNetPage?> PageAsync(int offset, CancellationToken cancellationToken)
        {
            Offsets.Add(offset);
            var index = Offsets.Count - 1;
            return Task.FromResult(index < pages.Length ? pages[index] : null);
        }
    }

    private static GalNetWatch Watch(Source source, GalNetArchive archive, Func<bool>? enabled = null)
    {
        var watch = new GalNetWatch(source, archive, enabled ?? (() => true), NullLogger<GalNetWatch>.Instance);
        watch.Dispatch = work => work().GetAwaiter().GetResult();
        return watch;
    }

    private static GalNetArchive Archive(MemoryFileSystem? files = null) =>
        new(ArchivePath, files ?? new MemoryFileSystem(), NullLogger<GalNetArchive>.Instance);

    private static GalNetPage Page(bool older, params GalNetArticle[] articles) => new(articles, older);

    [Fact]
    public void NothingIsDispatchedWhileTheSettingIsOff()
    {
        var source = new Source();
        var watch = Watch(source, Archive(), () => false);

        watch.Poll(Start);

        Assert.Empty(source.Offsets);
    }

    [Fact]
    public void ItFetchesOnTheFirstPollAndThenOnceAnHour()
    {
        var source = new Source(Page(false), Page(false), Page(false));
        var watch = Watch(source, Archive());

        watch.Poll(Start);
        Assert.Single(source.Offsets);

        watch.Poll(Start + TimeSpan.FromMinutes(59));
        Assert.Single(source.Offsets);

        watch.Poll(Start + TimeSpan.FromHours(1));
        Assert.Equal(2, source.Offsets.Count);
    }

    [Fact]
    public void TheFirstFetchReadsPagesBackToTwoYears()
    {
        var source = new Source(
            Page(true, Article("a", Start.AddDays(-1)), Article("b", Start.AddDays(-2))),
            Page(true, Article("c", Start.AddDays(-400)), Article("d", Start.AddDays(-500))),
            Page(true, Article("e", Start.AddDays(-700)), Article("f", Start.AddDays(-800))),
            Page(false, Article("g", Start.AddDays(-900))));
        var archive = Archive();
        var watch = Watch(source, archive);

        watch.Poll(Start);

        Assert.Equal([0, 2, 4], source.Offsets);
        Assert.Equal(["a", "b", "c", "d", "e"], archive.All().Select(article => article.Id));
    }

    [Fact]
    public void ALaterFetchWithOneNewArticleReadsOnePage()
    {
        var archive = Archive();
        archive.Merge([Article("a", Start.AddDays(-3)), Article("b", Start.AddDays(-4))]);
        var source = new Source(
            Page(true, Article("n", Start.AddMinutes(-5)), Article("a", Start.AddDays(-3))),
            Page(true, Article("b", Start.AddDays(-4))));
        var watch = Watch(source, archive);

        watch.Poll(Start);

        Assert.Equal([0], source.Offsets);
        Assert.Equal(["n", "a", "b"], archive.All().Select(article => article.Id));
    }

    [Fact]
    public void ArrivedIsEmptyAfterTheFirstBackfillAndHoldsTheNewArticleAfterward()
    {
        var source = new Source(
            Page(false, Article("a", Start.AddDays(-1))),
            Page(true, Article("n", Start.AddMinutes(-5)), Article("a", Start.AddDays(-1))));
        var watch = Watch(source, Archive());

        watch.Poll(Start);
        watch.Poll(Start);
        Assert.Empty(watch.Arrived);

        watch.Poll(Start + TimeSpan.FromHours(1));
        watch.Poll(Start + TimeSpan.FromHours(1));
        Assert.Equal(["n"], watch.Arrived.Select(article => article.Id));

        watch.Poll(Start + TimeSpan.FromHours(1));
        Assert.Empty(watch.Arrived);
    }

    [Fact]
    public void AFailedFetchLeavesTheArchiveAsItWas()
    {
        var archive = Archive();
        archive.Merge([Article("a", Start.AddDays(-3))]);
        var source = new Source(
            Page(true, Article("n", Start.AddMinutes(-5))),
            null);
        var watch = Watch(source, archive);

        watch.Poll(Start);

        Assert.Equal([0, 1], source.Offsets);
        Assert.Equal(["a"], archive.All().Select(article => article.Id));
        watch.Poll(Start);
        Assert.Empty(watch.Arrived);
    }

    [Fact]
    public void TheArchiveRoundTripsThroughTheFileSystem()
    {
        var files = new MemoryFileSystem();
        Archive(files).Merge([Article("a", Start.AddDays(-1)), Article("b", Start.AddDays(-2))]);

        var reread = Archive(files).All();

        Assert.Equal(["a", "b"], reread.Select(article => article.Id));
        Assert.Equal(["One.", "Two."], reread[0].Body);
        Assert.Equal("20 OCT 3312", reread[0].GameDate);
        Assert.Equal(Start.AddDays(-1), reread[0].Published);
    }

    [Fact]
    public void AnArticleMergedUnderAKnownIdReplacesTheStoredOne()
    {
        var archive = Archive();
        archive.Merge([Article("a", Start.AddDays(-1), "Old")]);

        var added = archive.Merge([Article("a", Start.AddDays(-1), "Edited")]);

        Assert.Empty(added);
        Assert.Equal("Edited", Assert.Single(archive.All()).Title);
    }

    [Fact]
    public void AnUnreadableFileIsKeptAsideRatherThanOverwritten()
    {
        var files = new MemoryFileSystem();
        files.WriteText(ArchivePath, "{ not an array");
        var archive = Archive(files);

        archive.Merge([Article("a", Start)]);

        Assert.Equal("{ not an array", files.ReadText(ArchivePath + ".unreadable"));
        Assert.Single(archive.All());
    }
}
