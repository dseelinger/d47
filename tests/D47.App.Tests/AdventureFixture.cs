using D47.Core.Storage;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The smallest adventure surface that furnishes a tab.</summary>
internal static class AdventureFixture
{
    public static AdventureSurface Surface(AppPaths? paths = null)
    {
        paths ??= new AppPaths(TestSurface.MemoryFolder("d47-adventure-fixture"));
        var folder = paths.Data;

        var store = new AdventureStore(
            Path.Combine(folder, "adventures.json"), TestSurface.FilesFor(paths), NullLogger<AdventureStore>.Instance);

        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);

        return new AdventureSurface(
            book,
            generator,
            () => null,
            () => "F1",
            () => new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            _ => { },
            () => false,
            () => false,
            () => null,
            () => { });
    }
}
