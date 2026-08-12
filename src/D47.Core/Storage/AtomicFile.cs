using System.Text;

namespace D47.Core.Storage;

/// <summary>
/// Writes go to a ".writing" sibling and then <see cref="File.Move(string,string,bool)"/>,
/// which is atomic on NTFS (architecture.md §5 D6). A crash mid-write leaves the previous
/// file intact rather than a half-written one.
/// </summary>
public static class AtomicFile
{
    public const string PendingSuffix = ".writing";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var pending = path + PendingSuffix;
        File.WriteAllText(pending, contents, Utf8NoBom);
        File.Move(pending, path, overwrite: true);
    }
}
