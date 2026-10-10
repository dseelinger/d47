using D47.Core;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests;

/// <summary>An install on a real temporary folder, for tests of disk behaviour.</summary>
[Trait("Category", "Integration")]
public sealed class TempInstall : IDisposable
{
    public TempInstall()
    {
        Root = Path.Combine(Path.GetTempPath(), "d47-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new AppPaths(Root);
        Paths.EnsureCreated();
    }

    public string Root { get; }

    public AppPaths Paths { get; }

    public IFileSystem Files { get; } = new DiskFileSystem();

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        // A leftover temp folder is not worth failing a test over.
        }
    }
}
