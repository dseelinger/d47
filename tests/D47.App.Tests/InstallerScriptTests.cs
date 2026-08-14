using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The installer script is the one piece of the shipped product the compiler never sees, so
/// the things it has to agree with the app about are asserted here instead. Both facts below
/// fail silently in the worst way: a wrong shortcut path gives the Commander two Start Menu
/// entries and an offer that should have stood down, and a flattened <c>runtimes</c> folder
/// gives them a d47 that starts perfectly and cannot transcribe a word — which is exactly the
/// failure that went unnoticed through thirteen releases.
/// </summary>
public class InstallerScriptTests
{
    private static string Script => File.ReadAllText(Path.Combine(RepositoryRoot(), "installer", "d47.iss"));

    /// <summary>
    /// The installer's Start Menu entry has to land on <see cref="StartMenuShortcut.DefaultPath"/>
    /// exactly — <c>{userprograms}</c> is the same folder — because that path is what the app's
    /// own "add a Start Menu entry?" offer tests before deciding to stay quiet.
    /// </summary>
    [Fact]
    public void TheInstallerWritesTheShortcutWhereTheAppLooksForIt()
    {
        // The script names the entry through a #define, so both halves are checked: that the
        // macro still holds the name the app looks for, and that the icon is written to the
        // per-user Start Menu folder using it.
        var declared = System.Text.RegularExpressions.Regex
            .Match(Script, @"#define\s+Name\s+""([^""]+)""")
            .Groups[1].Value;

        Assert.Equal(StartMenuShortcut.EntryName, declared);
        Assert.Contains(@"{userprograms}\{#Name}", Script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whisper's loader probes <c>runtimes\win-x64</c> on disk beside the executable, so the
    /// natives have to be installed with their folder structure rather than flattened.
    /// </summary>
    [Fact]
    public void TheInstallerKeepsTheNativeLibrariesInTheirFolders()
    {
        Assert.Contains(@"DestDir: ""{app}\runtimes""", Script, StringComparison.Ordinal);
        Assert.Contains("recursesubdirs", Script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Per-user and unelevated is a stated property of the product (architecture.md §9), and
    /// the flag that grants it is one word in a file nothing else checks.
    /// </summary>
    [Fact]
    public void TheInstallerNeverAsksForElevation()
    {
        Assert.Contains("PrivilegesRequired=lowest", Script, StringComparison.Ordinal);
    }

    /// <summary>
    /// A fixed install directory is what keeps <c>data\</c> beside the executable across an
    /// upgrade. A versioned one would strand the Commander's keys and models on every release.
    /// </summary>
    [Fact]
    public void TheInstallDirectoryDoesNotChangeWithTheVersion()
    {
        Assert.Contains(@"DefaultDirName={localappdata}\Programs\d47", Script, StringComparison.Ordinal);
        Assert.DoesNotContain(@"DefaultDirName={localappdata}\Programs\d47-{#Version}", Script, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
