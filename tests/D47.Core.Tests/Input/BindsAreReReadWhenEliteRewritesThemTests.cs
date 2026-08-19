using D47.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// Bindings are re-read when Elite rewrites them (remediation.md 16, item 2).
/// <para>
/// They used to be read once at startup, and the reason given was that the Commander cannot edit
/// their controls while d47 is the foreground window — true, and beside the point. Controls are
/// edited in <em>Elite's</em> options menu, with Elite in front, most often right after d47 has
/// said an action is not bound. From then until a restart, every injection sent the old scancode
/// and every answer about what is reachable described a preset no longer in use.
/// </para>
/// <para>
/// Real folders on disk, like <see cref="BindsTests"/>, because all of this is about which file
/// is chosen and when it is noticed to have moved.
/// </para>
/// </summary>
public class BindsAreReReadWhenEliteRewritesThemTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "d47-binds-watch-tests", Guid.NewGuid().ToString("N"));

    private string Bindings => Path.Combine(_root, "Options", "Bindings");

    private string Game => Path.Combine(_root, "Game");

    public BindsAreReReadWhenEliteRewritesThemTests()
    {
        Directory.CreateDirectory(Bindings);
        Directory.CreateDirectory(Game);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }

    private BindsWatch Watch() => new(Bindings, [Game], NullLogger.Instance);

    private void StartPreset(string fileName, string preset) =>
        Write(Path.Combine(Bindings, fileName), preset + "\n" + preset + "\n");

    private void Binds(string fileName, string action, string key) =>
        Write(
            Path.Combine(Bindings, fileName),
            $"""
             <Root PresetName="Test">
               <{action}>
                 <Primary Device="Keyboard" Key="{key}" />
               </{action}>
             </Root>
             """);

    /// <summary>
    /// Written with a distinct write time, because the whole mechanism is a comparison of write
    /// stamps and a test that rewrites a file inside one filesystem tick is testing nothing.
    /// </summary>
    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(Written++));
    }

    private static int Written;

    /// <summary>The startup read is unchanged. It was never the defect.</summary>
    [Fact]
    public void TheFirstReadHappensImmediately()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds("Custom.4.2.binds", "YawLeftButton", "Key_Q");

        var watch = Watch();

        Assert.Equal("Custom", watch.Current.PresetName);
        Assert.Equal("Key_Q", Assert.Single(watch.Current.Bindings).Key);
    }

    /// <summary>
    /// A tick where nothing moved re-reads nothing. This runs ten times a second for the life of
    /// the session, so it has to cost a stamp comparison and no XML.
    /// </summary>
    [Fact]
    public void APollWithNothingChangedDoesNotReRead()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds("Custom.4.2.binds", "YawLeftButton", "Key_Q");

        var watch = Watch();

        Assert.False(watch.Poll());
        Assert.False(watch.Poll());
    }

    /// <summary>
    /// The reported case: the Commander rebinds a control in the game's own options menu, Elite
    /// rewrites the file, and d47 knew nothing about it until a restart.
    /// </summary>
    [Fact]
    public void ARebindInTheGameIsPickedUp()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds("Custom.4.2.binds", "YawLeftButton", "Key_Q");

        var watch = Watch();

        Binds("Custom.4.2.binds", "YawLeftButton", "Key_Z");

        Assert.True(watch.Poll());
        Assert.Equal("Key_Z", Assert.Single(watch.Current.Bindings).Key);
    }

    /// <summary>
    /// <b>Two files, not one.</b> Switching preset rewrites <c>StartPreset</c> and changes which
    /// <c>.binds</c> file is the answer, so a watch on the resolved file alone would miss the
    /// larger of the two changes — the one that can move every binding at once.
    /// </summary>
    [Fact]
    public void SwitchingPresetIsPickedUpEvenThoughTheOldFileNeverMoved()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds("Custom.4.2.binds", "YawLeftButton", "Key_Q");
        Binds("Racing.4.2.binds", "YawLeftButton", "Key_R");

        var watch = Watch();
        Assert.Equal("Custom", watch.Current.PresetName);

        StartPreset("StartPreset.4.start", "Racing");

        Assert.True(watch.Poll());
        Assert.Equal("Racing", watch.Current.PresetName);
        Assert.Equal("Key_R", Assert.Single(watch.Current.Bindings).Key);
    }

    /// <summary>
    /// And having noticed a preset switch, it goes on watching the file it landed on. The stamp
    /// is taken again after resolving for exactly this: the one recorded before the re-read
    /// describes the file that is no longer in use.
    /// </summary>
    [Fact]
    public void AfterAPresetSwitchTheNewFileIsTheOneWatched()
    {
        StartPreset("StartPreset.4.start", "Custom");
        Binds("Custom.4.2.binds", "YawLeftButton", "Key_Q");
        Binds("Racing.4.2.binds", "YawLeftButton", "Key_R");

        var watch = Watch();

        StartPreset("StartPreset.4.start", "Racing");
        Assert.True(watch.Poll());
        Assert.False(watch.Poll());

        Binds("Racing.4.2.binds", "YawLeftButton", "Key_T");

        Assert.True(watch.Poll());
        Assert.Equal("Key_T", Assert.Single(watch.Current.Bindings).Key);
    }

    /// <summary>
    /// A Commander with no Elite install is a supported state, not a failure — and one that must
    /// not spend a parse on every tick forever.
    /// </summary>
    [Fact]
    public void NothingToReadIsQuiet()
    {
        var watch = Watch();

        Assert.False(watch.Current.IsKnown);
        Assert.False(watch.Poll());
    }

    /// <summary>
    /// Elite writing its bindings for the first time — a fresh install, or a Commander who has
    /// never opened the controls menu — is noticed without a restart.
    /// </summary>
    [Fact]
    public void BindingsAppearingLaterAreNoticed()
    {
        var watch = Watch();
        Assert.False(watch.Current.IsKnown);

        StartPreset("StartPreset.4.start", "Custom");
        Binds("Custom.4.2.binds", "YawLeftButton", "Key_Q");

        Assert.True(watch.Poll());
        Assert.True(watch.Current.IsKnown);
    }
}
