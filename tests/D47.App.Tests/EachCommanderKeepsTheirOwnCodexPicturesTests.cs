using System.Text;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public sealed class EachCommanderKeepsTheirOwnCodexPicturesTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private static readonly CodexPictureKey Key = new(101, 18);

    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    private sealed class Install
    {
        public string? Active { get; set; }

        public bool Shared { get; set; }

        public MemoryFileSystem Files { get; } = new();

        public CodexPictureStore Store { get; }

        public Install() =>
            Store = new CodexPictureStore(Files, Folder, () => false, NullLogger.Instance, commander: () => Active, shared: () => Shared);

        public void Save(string commander, string picture)
        {
            Active = commander;
            Store.SaveLocal(Key, Bytes(picture), Bytes("source " + picture), new CropSquare(0, 0, 600));
        }
    }

    [Fact]
    public void APictureSavedByOneCommanderIsStoredUnderTheirFrontierId()
    {
        var install = new Install();
        install.Save("F1", "a");

        Assert.Equal(Bytes("a"), install.Files.ReadBytes(Path.Combine(Folder, "local", "F1", Key.FileName)));
        Assert.Null(install.Files.ReadBytes(Path.Combine(Folder, "local", Key.FileName)));
    }

    [Fact]
    public void AnotherCommanderDoesNotSeeItWhileSharingIsOff()
    {
        var install = new Install();
        install.Save("F1", "a");
        install.Active = "F2";

        Assert.Empty(install.Store.LocalKeys());
        Assert.Null(install.Store.Local(Key));
        Assert.False(install.Store.HasLocal(Key));
    }

    [Fact]
    public void AnotherCommandersPictureShowsWhenSharingIsOnAndTheyHaveNone()
    {
        var install = new Install { Shared = true };
        install.Save("F1", "a");
        install.Active = "F2";

        Assert.Contains(Key, install.Store.LocalKeys());
        Assert.Equal(Bytes("a"), install.Store.Local(Key));
        Assert.False(install.Store.HasLocal(Key));
        Assert.Null(install.Store.Own(Key));
    }

    [Fact]
    public void TheActiveCommandersPictureWinsWhenSharingIsOn()
    {
        var install = new Install { Shared = true };
        install.Save("F1", "a");
        install.Save("F2", "b");

        Assert.Equal(Bytes("b"), install.Store.Local(Key));

        install.Active = "F1";

        Assert.Equal(Bytes("a"), install.Store.Local(Key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingActsOnTheActiveCommandersPictureOnly(bool shared)
    {
        var install = new Install { Shared = shared };
        install.Save("F1", "a");
        install.Save("F2", "b");

        install.Store.RemoveLocal(Key);

        Assert.False(install.Store.HasLocal(Key));
        Assert.False(install.Store.HasLocalSource(Key));
        Assert.Null(install.Store.LocalCrop(Key));

        install.Active = "F1";

        Assert.True(install.Store.HasLocal(Key));
        Assert.Equal(Bytes("source a"), install.Store.LocalSource(Key));
        Assert.Equal(new CropSquare(0, 0, 600), install.Store.LocalCrop(Key));
    }

    [Fact]
    public void SavingReplacesOnlyTheActiveCommandersPicture()
    {
        var install = new Install { Shared = true };
        install.Save("F1", "a");
        install.Save("F2", "b");
        install.Save("F2", "c");

        Assert.Equal(Bytes("c"), install.Store.Local(Key));

        install.Active = "F1";

        Assert.Equal(Bytes("a"), install.Store.Local(Key));
    }

    [Fact]
    public void PicturesWithNoOwnerMoveToTheFirstCommanderReadAndStayThere()
    {
        var install = new Install();
        var local = Path.Combine(Folder, "local");
        install.Files.WriteBytes(Path.Combine(local, Key.FileName), Bytes("old"));
        install.Files.WriteBytes(Path.Combine(local, Key.SourceFileName), Bytes("old source"));
        install.Files.WriteText(Path.Combine(local, Key.CropFileName), new CropSquare(10, 20, 500).Write());

        install.Active = "F1";

        Assert.Equal(Bytes("old"), install.Store.Local(Key));
        Assert.Equal(Bytes("old source"), install.Store.LocalSource(Key));
        Assert.Equal(new CropSquare(10, 20, 500), install.Store.LocalCrop(Key));
        Assert.Null(install.Files.ReadBytes(Path.Combine(local, Key.FileName)));
        Assert.Null(install.Files.ReadBytes(Path.Combine(local, Key.SourceFileName)));
        Assert.Null(install.Files.ReadText(Path.Combine(local, Key.CropFileName)));

        install.Active = "F2";

        Assert.Empty(install.Store.LocalKeys());
        Assert.Equal(Bytes("old"), install.Files.ReadBytes(Path.Combine(local, "F1", Key.FileName)));
    }

    [Fact]
    public void WithNoCommanderActiveNoPictureShowsAndSavingIsNotOffered()
    {
        var install = new Install { Shared = true };
        install.Save("F1", "a");
        install.Active = null;

        Assert.False(install.Store.CanSave);
        Assert.Empty(install.Store.LocalKeys());
        Assert.Null(install.Store.Local(Key));
        Assert.False(install.Store.HasLocal(Key));
        Assert.Throws<InvalidOperationException>(() => install.Store.SaveLocal(Key, Bytes("x"), Bytes("y"), new CropSquare(0, 0, 600)));
    }
}
