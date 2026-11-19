using System.Text;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A Commander's pictures named with an old star or planet EntryID are renamed to the kind key on first read (#2603).</summary>
[Trait("Category", "Integration")]
public sealed class OldStarAndPlanetPicturesAreRenamedToKindsTests
{
    private const string Kind = "star.k_yellow_orange_star.dwarf";

    private static readonly string[] Old = ["1100601-18.jpg", "1100601-18.source.jpg", "1100601-18.crop.json"];

    private static readonly string[] New = [Kind + "-18.jpg", Kind + "-18.source.jpg", Kind + "-18.crop.json"];

    private static CodexPictureStore Store(string folder) =>
        new(new DiskFileSystem(), folder, () => false, NullLogger.Instance, commander: () => "F1");

    private static string Own(string folder) => Path.Combine(folder, CodexPictureStore.LocalFolder, "F1");

    private static List<string> Names(string folder) =>
        [.. Directory.EnumerateFiles(Own(folder)).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];

    private static void Write(string folder, string name, string text)
    {
        Directory.CreateDirectory(Own(folder));
        File.WriteAllText(Path.Combine(Own(folder), name), text, Encoding.ASCII);
    }

    private static string Read(string folder, string name) => File.ReadAllText(Path.Combine(Own(folder), name));

    [Fact]
    public void ThePictureSourceAndCropAreRenamedToTheKindKey()
    {
        var folder = TempFolders.Create("d47-codex-rekey");

        foreach (var name in Old)
        {
            Write(folder, name, name);
        }

        Write(folder, "2420101-18.jpg", "biology");

        var keys = Store(folder).LocalKeys();

        Assert.Contains(new CodexPictureKey(Kind, 18), keys);
        Assert.Equal(New.Append("2420101-18.jpg").Order(StringComparer.Ordinal), Names(folder));

        for (var i = 0; i < Old.Length; i++)
        {
            Assert.Equal(Old[i], Read(folder, New[i]));
        }
    }

    [Fact]
    public void ASecondReadRenamesNothing()
    {
        var folder = TempFolders.Create("d47-codex-rekey");

        foreach (var name in Old)
        {
            Write(folder, name, name);
        }

        var store = Store(folder);
        store.LocalKeys();
        var after = Names(folder);

        Write(folder, Old[0], "later");
        store.LocalKeys();

        Assert.Equal(after.Append(Old[0]).Order(StringComparer.Ordinal), Names(folder));
        Assert.Equal("later", Read(folder, Old[0]));
        Assert.Equal(Old[0], Read(folder, New[0]));
    }

    [Fact]
    public void AFileWhoseNewNameIsTakenIsLeftAsItIs()
    {
        var folder = TempFolders.Create("d47-codex-rekey");
        Write(folder, Old[0], "old");
        Write(folder, New[0], "kind");

        Store(folder).LocalKeys();

        Assert.Equal("old", Read(folder, Old[0]));
        Assert.Equal("kind", Read(folder, New[0]));
    }
}
