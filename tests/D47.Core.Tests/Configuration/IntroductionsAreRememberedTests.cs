using D47.Core;
using D47.Core.Configuration;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// Introductions survive a restart, and they do it through view state rather than through
/// settings (docs/plans/change-requests.md item 7).
/// <para>
/// The store is the real one against a temporary folder, because the claim being made is about
/// what reaches the disk and comes back — a fake would assert the adapter agrees with itself.
/// </para>
/// </summary>
public class IntroductionsAreRememberedTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-introductions-tests",
        Guid.NewGuid().ToString("n"));

    private ViewStateStore Store() =>
        new(new AppPaths(_root), NullLogger<ViewStateStore>.Instance);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // A temporary folder that outlives the run is not a test failure.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ACoreThatHasIntroducedItselfIsStillIntroducedNextLaunch()
    {
        var first = new PersonaHost(memory: new ViewStateIntroductions(Store()));
        first.Apply(new PersonaSettings { Id = "cora" });

        // A second store over the same folder, which is what the next launch has.
        var next = new PersonaHost(memory: new ViewStateIntroductions(Store()));

        Assert.Equal(["Cora"], next.Introduced.Select(p => p.Name));
    }

    /// <summary>
    /// It goes in <c>view-state.json</c>, not <c>settings.json</c>. Settings are append-only, so
    /// a property put there can never be taken out again — a heavy promise for a list of ids
    /// nobody configures.
    /// </summary>
    [Fact]
    public void ItIsKeptInViewStateAndNotInSettings()
    {
        var paths = new AppPaths(_root);

        var host = new PersonaHost(memory: new ViewStateIntroductions(Store()));
        host.Apply(new PersonaSettings { Id = "kex" });

        Assert.Contains("kex", File.ReadAllText(paths.ViewStateFile), StringComparison.Ordinal);

        Assert.True(
            !File.Exists(paths.SettingsFile)
            || !File.ReadAllText(paths.SettingsFile).Contains("introduc", StringComparison.OrdinalIgnoreCase),
            "introductions reached settings.json, where nothing can ever be removed from");
    }

    /// <summary>
    /// Writing introductions must not cost whatever else view state was holding. The adapter
    /// re-reads and amends rather than keeping a copy of the record, and this is that claim.
    /// </summary>
    [Fact]
    public void WritingIntroductionsLeavesTheRestOfViewStateAlone()
    {
        var store = Store();
        store.Save(store.Load() with { StartMenuOffered = true, CollapsedCards = ["listening"] });

        var host = new PersonaHost(memory: new ViewStateIntroductions(Store()));
        host.Apply(new PersonaSettings { Id = "cora" });

        var after = Store().Load();

        Assert.True(after.StartMenuOffered);
        Assert.Equal(["listening"], after.CollapsedCards);
        Assert.Equal(["cora"], after.IntroducedCores);
    }
}
