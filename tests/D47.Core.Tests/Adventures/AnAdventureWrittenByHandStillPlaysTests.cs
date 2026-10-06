using D47.Core.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>An adventure already on file with source <c>commander</c>, or with no source, loads and begins.</summary>
public sealed class AnAdventureWrittenByHandStillPlaysTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 20, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-hand-written-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private AdventureBook Book()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "adventures.json");
        File.Copy(Path.Combine(RepositoryRoot(), "tests", "fixtures", "adventures", "hand-written.json"), path);

        var store = new AdventureStore(path, NullLogger<AdventureStore>.Instance);
        Assert.True(store.Poll());

        return new AdventureBook(store, NullLogger<AdventureBook>.Instance);
    }

    [Theory]
    [InlineData("the-lantern-route")]
    [InlineData("the-quiet-dock")]
    public void ItLoadsBeginsAndOffersAbandonAndRemove(string key)
    {
        var book = Book();

        Assert.Empty(book.Store.Problems);

        var before = book.Standing("F1", key)!;

        Assert.Equal(AdventureSource.Commander, before.Adventure.Source);
        Assert.Equal(["Begin", "Remove"], before.ReadingButtons());

        Assert.Null(book.Begin("F1", key, Now));

        var after = book.Standing("F1", key)!;

        Assert.True(after.Adventure.IsActive);
        Assert.Equal(["Abandon", "Remove"], after.ReadingButtons());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
    }
}
