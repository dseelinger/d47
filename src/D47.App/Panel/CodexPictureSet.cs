using D47.Core.Storage;

namespace D47.App.Panel;

/// <summary>What the codex entry page draws pictures from: the store, the stock setting and the shipped family placeholders.</summary>
public sealed class CodexPictureSet(CodexPictureStore store, Func<bool> stockOn, IFileSystem files, string placeholders)
{
    public CodexPictureStore Store => store;

    public bool StockOn => stockOn();

    /// <summary>The bytes of a family placeholder; null when the file is not there.</summary>
    public byte[]? Placeholder(string file) => files.ReadBytes(Path.Combine(placeholders, file));
}
