namespace D47.Knowledge.Tests;

internal sealed class TempFile : IDisposable
{
    public TempFile()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d47-tests");

        Directory.CreateDirectory(folder);

        Path = System.IO.Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json");
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
        // A leftover temp file is not worth failing a test over.
        }
    }
}
