using D47.App;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// d47 does not install, so nothing puts it anywhere findable. The shortcut is the cheapest fix
/// that does not turn it into an installer — and because it writes outside the data folder, it
/// only ever happens on an explicit yes.
/// <para>
/// Every test here writes into a temporary folder. Nothing touches the real Start Menu.
/// </para>
/// </summary>
public class StartMenuShortcutTests
{
    [Fact]
    public void AShortcutIsWrittenPointingAtTheExecutable()
    {
        var folder = Directory.CreateTempSubdirectory("d47-shortcut-tests").FullName;

        // A real file to point at; the shell resolves the target when the link is saved.
        var target = Path.Combine(folder, "d47.exe");
        File.WriteAllText(target, "not really an executable, but a real file");

        var link = Path.Combine(folder, "Directive 47.lnk");

        Assert.True(StartMenuShortcut.TryCreate(link, target, NullLogger.Instance));

        Assert.True(File.Exists(link));
        Assert.True(new FileInfo(link).Length > 0);
    }

    /// <summary>The folder is created rather than assumed — a fresh profile may not have one.</summary>
    [Fact]
    public void AMissingFolderIsCreatedRatherThanFailing()
    {
        var folder = Directory.CreateTempSubdirectory("d47-shortcut-tests").FullName;

        var target = Path.Combine(folder, "d47.exe");
        File.WriteAllText(target, "not really an executable");

        var link = Path.Combine(folder, "Programs", "Nested", "Directive 47.lnk");

        Assert.True(StartMenuShortcut.TryCreate(link, target, NullLogger.Instance));
        Assert.True(File.Exists(link));
    }

    /// <summary>
    /// Failing to write a convenience is not a failure of the program: it reports and the
    /// Commander runs d47 exactly the way they just did.
    /// </summary>
    [Fact]
    public void AnImpossiblePathIsReportedRatherThanThrown()
    {
        Assert.False(StartMenuShortcut.TryCreate(
            Path.Combine(Path.GetTempPath(), "d47-tests\0invalid", "x.lnk"),
            Path.Combine(Path.GetTempPath(), "d47.exe"),
            NullLogger.Instance));
    }

    /// <summary>It goes to the per-user Start Menu, because the all-users one needs elevation.</summary>
    [Fact]
    public void TheDefaultPathIsThePerUserStartMenu()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

        Assert.StartsWith(programs, StartMenuShortcut.DefaultPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("Directive 47.lnk", StartMenuShortcut.DefaultPath, StringComparison.Ordinal);
    }

    /// <summary>
    /// The answer sticks whichever way it went. An offer that returns every launch is not an
    /// offer, and "no" is the answer it would be most annoying to forget.
    /// </summary>
    [Fact]
    public void TheAnswerIsRememberedAcrossARestart()
    {
        var root = Directory.CreateTempSubdirectory("d47-shortcut-state").FullName;
        var paths = new D47.Core.AppPaths(root);
        paths.EnsureCreated();

        var store = new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance);

        Assert.False(store.Load().StartMenuOffered);

        store.Save(store.Load() with { StartMenuOffered = true });

        Assert.True(store.Load().StartMenuOffered);
    }

    /// <summary>
    /// Recording the answer must not discard what else is in the file — the window's placement
    /// shares it, and losing that would be a visible regression from an invisible feature.
    /// </summary>
    [Fact]
    public void RememberingTheAnswerKeepsTheRestOfTheViewState()
    {
        var root = Directory.CreateTempSubdirectory("d47-shortcut-state").FullName;
        var paths = new D47.Core.AppPaths(root);
        paths.EnsureCreated();

        var store = new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance);

        store.Save(new ViewState
        {
            MainWindow = new WindowPlacement { Width = 900, Height = 700 },
            CollapsedCards = ["speech"],
        });

        store.Save(store.Load() with { StartMenuOffered = true });

        var state = store.Load();

        Assert.True(state.StartMenuOffered);
        Assert.Equal(900, state.MainWindow!.Width);
        Assert.Equal(["speech"], state.CollapsedCards);
    }
}
