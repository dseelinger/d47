using D47.Core.Interface;
using D47.Core.Storage;

namespace D47.App.Panel;

/// <summary>What the codex entry page draws pictures from: the store, the stock setting, the shipped family placeholders, the captures to cut a picture from and the maintainer's publisher.</summary>
public sealed class CodexPictureSet(
    CodexPictureStore store, Func<bool> stockOn, IFileSystem files, string placeholders, string? captures = null, CodexPicturePublisher? publisher = null)
{
    public CodexPictureStore Store => store;

    public CodexPicturePublisher? Publisher => publisher;

    public bool StockOn => stockOn();

    /// <summary>The bytes of a family placeholder; null when the file is not there.</summary>
    public byte[]? Placeholder(string file) => files.ReadBytes(Path.Combine(placeholders, file));

    /// <summary>The kept captures, newest first, those noted with <paramref name="key"/> ahead of the rest.</summary>
    public IReadOnlyList<CodexCapture> Captures(CodexPictureKey key) =>
        captures is null ? [] : CodexCaptures.List(files, captures, key);

    /// <summary>The bytes of a capture; null when it has been pruned.</summary>
    public byte[]? CaptureBytes(string path) => files.ReadBytes(path);
}
