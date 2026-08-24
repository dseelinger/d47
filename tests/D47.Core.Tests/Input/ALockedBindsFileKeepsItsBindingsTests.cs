using System.IO;
using D47.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// Elite holding a bindings file open does not cost the Commander their bindings
/// (<a href="https://github.com/dseelinger/d47/issues/24">#24</a>).
/// <para>
/// <b>What happened on 2026-08-24.</b> 347 bindings were loaded at 08:00:30. At 08:28:57 Elite had
/// <c>StartPreset.4.start</c> open for a moment, the read threw, the resolver reported it as
/// <em>"No StartPreset file"</em>, and the watcher recorded <em>"re-read 0 bindings from preset
/// none"</em>. Nothing asked again. Every key d47 can press was dead until the app was restarted at
/// 11:09 — two hours and forty-one minutes — and the only thing that mentioned it was an arrival
/// honk saying it did not know which keys the Commander uses.
/// </para>
/// <para>
/// <b>The lock is taken for real rather than faked.</b> A test double for "this read throws" would
/// assert that the code does what it was written to do; an actual <see cref="FileShare.None"/>
/// handle asserts that the thing Elite does produces the outcome a Commander needs.
/// </para>
/// </summary>
public class ALockedBindsFileKeepsItsBindingsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "d47-locked-binds-tests", Guid.NewGuid().ToString("N"));

    private string Bindings => Path.Combine(_root, "Options", "Bindings");

    private string Game => Path.Combine(_root, "Game");

    public void Dispose() => GC.SuppressFinalize(this);

    private void StartPreset(string name) =>
        File.WriteAllText(Path.Combine(Bindings, name), "Custom\nCustom\n");

    private void Binds(string key)
    {
        var body =
            "<Root PresetName=\"Custom\">"
            + "<YawLeftButton>"
            + $"<Primary Device=\"Keyboard\" Key=\"{key}\" />"
            + "<Secondary Device=\"{NoDevice}\" Key=\"\" />"
            + "</YawLeftButton>"
            + "</Root>";

        File.WriteAllText(Path.Combine(Bindings, "Custom.4.2.binds"), body);
    }

    private BindsWatch Watch() => new(Bindings, [Game], NullLogger.Instance);

    /// <summary>Holds the preset file exactly as Elite does while it writes one.</summary>
    private FileStream Lock() =>
        new(Path.Combine(Bindings, "StartPreset.4.start"), FileMode.Open, FileAccess.Read, FileShare.None);

    [Fact]
    public void AFileLockedWhileItIsPolledDoesNotCostTheBindingsAlreadyLoaded()
    {
        Directory.CreateDirectory(Bindings);
        StartPreset("StartPreset.4.start");
        Binds("Key_Q");

        var watch = Watch();

        Assert.Single(watch.Current.Bindings);
        Assert.Equal("Custom", watch.Current.PresetName);

        // Elite rewrites the preset file and holds it while it does. The touch is what makes the
        // watcher look; the lock is what makes the look fail.
        File.SetLastWriteTimeUtc(
            Path.Combine(Bindings, "StartPreset.4.start"),
            DateTime.UtcNow.AddMinutes(1));

        using (Lock())
        {
            Assert.False(watch.Poll(), "a read that could not be made is not a reload");

            Assert.Single(watch.Current.Bindings);
            Assert.Equal(
                "Custom",
                watch.Current.PresetName);
        }
    }

    [Fact]
    public void TheReadIsTriedAgainOnceTheFileIsLetGoOf()
    {
        Directory.CreateDirectory(Bindings);
        StartPreset("StartPreset.4.start");
        Binds("Key_Q");

        var watch = Watch();

        // A rebind: the file moves and its content changes, so a successful read must land on the
        // new key rather than on the one already held.
        Binds("Key_Z");

        File.SetLastWriteTimeUtc(
            Path.Combine(Bindings, "StartPreset.4.start"),
            DateTime.UtcNow.AddMinutes(1));

        using (Lock())
        {
            Assert.False(watch.Poll(), "locked");
            Assert.False(watch.Poll(), "still locked");
        }

        // The lock is a moment and the file never moves again, so the next read has to come from
        // the watcher trying rather than from another change notification — which is the half of
        // #24 that made a transient fault permanent.
        Assert.True(watch.Poll(), "the read is tried again once the file is readable");

        Assert.Equal("Key_Z", watch.Current.Bindings[0].Key);
    }

    [Fact]
    public void ALockedFileIsNotReportedAsAMissingOne()
    {
        Directory.CreateDirectory(Bindings);
        StartPreset("StartPreset.4.start");
        Binds("Key_Q");

        using (Lock())
        {
            Assert.Null(BindsResolver.ActivePresetName(Bindings, NullLogger.Instance, out var locked));
            Assert.True(locked, "a file something else has open is not a file that is absent");
        }

        Assert.Equal("Custom", BindsResolver.ActivePresetName(Bindings, NullLogger.Instance, out var free));
        Assert.False(free);
    }

    /// <summary>
    /// The other half of the bargain: a Commander who genuinely has no bindings still gets the
    /// honest answer, and a fix that kept stale bindings for ever would be its own defect.
    /// </summary>
    [Fact]
    public void NoStartPresetAtAllIsStillAnsweredAsNoBindings()
    {
        Directory.CreateDirectory(Bindings);

        var watch = Watch();

        Assert.Empty(watch.Current.Bindings);
        Assert.False(watch.Current.IsKnown);
    }
}
