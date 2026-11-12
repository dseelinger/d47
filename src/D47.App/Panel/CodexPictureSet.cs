using D47.Core.Interface;
using D47.Core.Storage;

namespace D47.App.Panel;

/// <summary>What the codex entry page draws pictures from: the store, the stock setting, the shipped family placeholders, the screenshots to cut a picture from and the maintainer's publisher.</summary>
public sealed class CodexPictureSet(
    CodexPictureStore store, Func<bool> stockOn, IFileSystem files, string placeholders, string? captures = null, CodexPicturePublisher? publisher = null)
{
    public CodexPictureStore Store => store;

    /// <summary>The Commander saved a picture of their own for the entry from a new capture; a re-crop does not raise it.</summary>
    public event Action<CodexPictureKey>? PictureSaved;

    internal void RaisePictureSaved(CodexPictureKey key) => PictureSaved?.Invoke(key);

    public CodexPicturePublisher? Publisher => publisher;

    public bool StockOn => stockOn();

    /// <summary>The bytes of a family placeholder; null when the file is not there.</summary>
    public byte[]? Placeholder(string file) => files.ReadBytes(Path.Combine(placeholders, file));

    /// <summary>The screenshots, newest first, those noted with <paramref name="key"/> ahead of the rest.</summary>
    public IReadOnlyList<CodexCapture> Captures(CodexPictureKey key) =>
        captures is null ? [] : CodexCaptures.List(files, captures, key);

    /// <summary>The gamma a picture cut from the capture at <paramref name="path"/> is lifted with, from its note; null when it is not lifted.</summary>
    public double? CaptureGamma(string path) =>
        PictureLift.For(PictureNote.Parse(files.ReadText(Path.ChangeExtension(path, ".json")))?.Source);

    /// <summary>The bytes of a screenshot; null when it has gone.</summary>
    public byte[]? CaptureBytes(string path) => files.ReadBytes(path);
}
