using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Storage;

public sealed class AMemoryFileSystemKeepsTheDisksRulesTests
{
    private const string Folder = @"C:\d47-memory\data";

    private readonly MemoryFileSystem _files = new();

    [Fact]
    public void AWrittenFileReadsBackAndMissingOnesReadAsNull()
    {
        var path = Path.Combine(Folder, "a.json");

        _files.WriteText(path, "{}");

        Assert.Equal("{}", _files.ReadText(path));
        Assert.Null(_files.ReadText(Path.Combine(Folder, "b.json")));
        Assert.Null(_files.Stat(Path.Combine(Folder, "b.json")));
        Assert.Null(_files.OpenRead(Path.Combine(Folder, "b.json")));
    }

    [Fact]
    public void ARewriteOfTheSameLengthMovesTheStamp()
    {
        var path = Path.Combine(Folder, "a.json");

        _files.WriteText(path, "one");
        var first = _files.Stat(path);

        _files.WriteText(path, "two");
        var second = _files.Stat(path);

        Assert.Equal(first?.Length, second?.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void AnAppendCreatesTheFileAndItsFolder()
    {
        var path = Path.Combine(Folder, "log", "spend.jsonl");

        Assert.Null(_files.FolderWritten(Path.Combine(Folder, "log")));

        _files.AppendText(path, "one\n");

        Assert.Equal("one\n", _files.ReadText(path));
        Assert.NotNull(_files.FolderWritten(Path.Combine(Folder, "log")));
        Assert.NotNull(_files.FolderWritten(Folder));
    }

    [Fact]
    public void AnAppendAddsToTheTextAndMovesTheStamp()
    {
        var path = Path.Combine(Folder, "spend.jsonl");

        _files.AppendText(path, "one\n");
        var first = _files.Stat(path);

        _files.AppendText(path, "two\n");

        Assert.Equal("one\ntwo\n", _files.ReadText(path));
        Assert.NotEqual(first?.Written, _files.Stat(path)?.Written);
    }

    [Fact]
    public void TheFoldersStampMovesWhenAFileIsCreatedInIt()
    {
        _files.WriteText(Path.Combine(Folder, "a.json"), "a");
        var before = _files.FolderWritten(Folder);

        _files.WriteText(Path.Combine(Folder, "a.json"), "b");

        Assert.Equal(before, _files.FolderWritten(Folder));

        _files.WriteText(Path.Combine(Folder, "b.json"), "b");

        Assert.NotEqual(before, _files.FolderWritten(Folder));
    }

    [Fact]
    public void AnEnumerateOnAMissingFolderReturnsEmpty()
    {
        Assert.Empty(_files.Enumerate(Path.Combine(Folder, "nowhere"), "*.json"));
    }

    [Fact]
    public void AnEnumerateMatchesThePatternAndRecursesOnlyWhenAsked()
    {
        _files.WriteText(Path.Combine(Folder, "b.json"), "b");
        _files.WriteText(Path.Combine(Folder, "a.json"), "a");
        _files.WriteText(Path.Combine(Folder, "notes.txt"), "n");
        _files.WriteText(Path.Combine(Folder, "deep", "c.json"), "c");
        _files.WriteText(Path.Combine(Folder + "-other", "d.json"), "d");

        Assert.Equal(
            [Path.Combine(Folder, "a.json"), Path.Combine(Folder, "b.json")],
            _files.Enumerate(Folder, "*.json"));

        Assert.Equal(
            [Path.Combine(Folder, "a.json"), Path.Combine(Folder, "b.json"), Path.Combine(Folder, "deep", "c.json")],
            _files.Enumerate(Folder, "*.json", recursive: true));
    }

    [Fact]
    public void PathsCompareIgnoringCase()
    {
        _files.WriteText(Path.Combine(Folder, "Settings.json"), "x");

        Assert.Equal("x", _files.ReadText(Path.Combine(Folder.ToUpperInvariant(), "SETTINGS.JSON")));
        Assert.Single(_files.Enumerate(Folder.ToLowerInvariant(), "settings.*"));
    }

    [Fact]
    public void TheLengthIsTheUtf8ByteCount()
    {
        var path = Path.Combine(Folder, "a.txt");

        _files.WriteText(path, "Ré");

        Assert.Equal(3, _files.Stat(path)?.Length);
    }

    [Fact]
    public void ALeadingByteOrderMarkIsNotPartOfTheText()
    {
        var path = Path.Combine(Folder, "a.json");

        _files.WriteText(path, "\uFEFF{}");

        Assert.Equal("{}", _files.ReadText(path));
    }

    [Fact]
    public void AnOpenedStreamIsUnchangedByALaterWrite()
    {
        var path = Path.Combine(Folder, "a.txt");
        _files.WriteText(path, "first");

        using var stream = _files.OpenRead(path)!;
        _files.WriteText(path, "second");

        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal("first", new StreamReader(stream).ReadToEnd());
    }

    [Fact]
    public void ACopyOverwritesTheDestinationAndAMissingSourceThrows()
    {
        var from = Path.Combine(Folder, "settings.json");
        var to = Path.Combine(Folder, "backup", "settings.json");

        _files.WriteText(from, "new");
        _files.WriteText(to, "old");
        _files.Copy(from, to);

        Assert.Equal("new", _files.ReadText(to));
        Assert.Equal(_files.Stat(from), _files.Stat(to));
        Assert.Throws<FileNotFoundException>(() => _files.Copy(Path.Combine(Folder, "missing.json"), to));
    }

    [Fact]
    public void ADeleteRemovesTheFileAndMovesTheFoldersStamp()
    {
        var path = Path.Combine(Folder, "a.json");
        _files.WriteText(path, "a");
        var before = _files.FolderWritten(Folder);

        _files.Delete(path);

        Assert.Null(_files.Stat(path));
        Assert.NotEqual(before, _files.FolderWritten(Folder));
    }

    [Fact]
    public void ADeleteOfAMissingFileOrFolderDoesNothing()
    {
        _files.Delete(Path.Combine(Folder, "nowhere.json"));
        _files.Delete(Path.Combine(Folder, "no-such-folder", "a.json"));

        Assert.Null(_files.FolderWritten(Folder));
    }
}
