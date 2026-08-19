using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Memory;

/// <summary>
/// The Commander can make d47 forget them, and nothing else can (remediation.md 11, item 10).
/// <para>
/// The control was built in Phase 31 and had no test of its own. It is the one action in this
/// repository that cannot be undone and the one whose whole value is that it can be trusted, so
/// "it is written down somewhere that this is protected" is the weakest form that claim can take.
/// </para>
/// </summary>
public class MemoryCanBeErasedOnDemandTests
{
    private const string Row = "privacy.memory";

    private static SettingRow Erase(TestSurface surface) =>
        surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Single(row => row.Key == Row);

    /// <summary>It exists, it is pressable, and the button says what it does.</summary>
    [Fact]
    public void ThereIsAButtonForIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = Erase(surface);

        Assert.NotNull(row.Press);
        Assert.Equal("Forget everything", row.PressLabel);
    }

    /// <summary>
    /// And it empties the store — every Commander in the file, because a Commander pressing this
    /// means "forget me" rather than "forget the character I happen to be logged in as".
    /// </summary>
    [Fact]
    public void PressingItForgetsEverybody()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var memories = surface.Memories;

        memories.Store.Write("F1", Entry("likes-the-krait", "The Commander flies a Krait."));
        memories.Store.Write("F2", Entry("hates-mining", "The Commander does not mine."));

        Assert.NotEmpty(memories.Store.For("F1"));
        Assert.NotEmpty(memories.Store.For("F2"));

        Erase(surface).Press!();

        Assert.Empty(memories.Store.For("F1"));
        Assert.Empty(memories.Store.For("F2"));
    }

    /// <summary>
    /// It survives a restart, which is the half that matters: an erase that emptied the copy in
    /// memory and left the file alone would look identical until the next launch.
    /// </summary>
    [Fact]
    public void AndItIsGoneFromTheFileToo()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Memories.Store.Write("F1", Entry("likes-the-krait", "The Commander flies a Krait."));

        Erase(surface).Press!();

        var reopened = TestSurface.For(install);

        Assert.Empty(reopened.Memories.Store.For("F1"));
    }

    /// <summary>
    /// <b>The protection, stated as an assertion.</b> The row is Info with a Press, which is what
    /// makes <see cref="SettingsService.Apply"/> refuse it — so the model cannot reach it however
    /// it words the request, and neither can anything a hostile in-game message can make the model
    /// say.
    /// </summary>
    [Fact]
    public void TheModelCannotPressIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Memories.Store.Write("F1", Entry("likes-the-krait", "The Commander flies a Krait."));

        var refused = surface.Settings.Apply(Row, "true", SettingsCaller.Model);

        Assert.False(refused.Ok);
        Assert.NotEmpty(surface.Memories.Store.For("F1"));
    }

    /// <summary>
    /// And it is not in the model's vocabulary at all, which is the difference between a request
    /// that is refused and one that is never made.
    /// </summary>
    [Fact]
    public void ItIsNotOfferedToTheModelEither()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Equal(SettingKind.Info, Erase(surface).Kind);
    }

    /// <summary>
    /// <b>No spoken route, deliberately.</b> "Forget everything about me" is a sentence a
    /// transcriber can produce out of a misheard one, and this is the only action here that cannot
    /// be undone — so it is a press the Commander makes with their hand, and the absence of a
    /// phrase is a decision rather than an omission.
    /// </summary>
    [Fact]
    public void NoPhraseReachesIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        foreach (var said in new[]
                 {
                     "forget everything about me",
                     "forget me",
                     "forget everything",
                     "empty your memory",
                 })
        {
            Assert.Null(surface.Router.MatchSetting(said));
            Assert.Null(surface.Router.MatchToolCommand(said));
        }
    }

    private static D47.Core.Memory.MemoryEntry Entry(string key, string fact) =>
        new(key, fact) { Tier = D47.Core.Memory.MemoryTier.Stated };
}
