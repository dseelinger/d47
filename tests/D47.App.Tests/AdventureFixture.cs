using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests;

/// <summary>The smallest adventure surface that furnishes a tab.</summary>
internal static class AdventureFixture
{
    public static AdventureSurface Surface(AppPaths? paths = null)
    {
        var folder = paths?.Data
                     ?? TempFolders.Create("d47-adventure-fixture");

        Directory.CreateDirectory(folder);

        var store = new AdventureStore(
            Path.Combine(folder, "adventures.json"), NullLogger<AdventureStore>.Instance);

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
