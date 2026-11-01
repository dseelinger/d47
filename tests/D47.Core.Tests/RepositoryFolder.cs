using Xunit;

namespace D47.Core.Tests;

/// <summary>Locates the repository root by the <c>d47.slnx</c> above the test binary.</summary>
[Trait("Category", "Gate")]
internal static class RepositoryFolder
{
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
    }
}
