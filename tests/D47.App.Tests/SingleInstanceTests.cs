using D47.App;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// One d47 per Commander. A second copy is a second journal tailer, a second microphone, a
/// second set of global hotkeys and two writers over one data folder — none of which is visible
/// until something behaves oddly.
/// </summary>
public class SingleInstanceTests
{
    /// <summary>
    /// A slot of this test's own. The real one is per-user and process-wide, so using it would
    /// mean these tests failing whenever d47 happens to be open — which is precisely when
    /// somebody is testing.
    /// </summary>
    private static string Slot() => @"Local\" + $"d47-tests-{Guid.NewGuid():N}";

    [Fact]
    public void TheSecondCopyDoesNotGetTheSlot()
    {
        var slot = Slot();

        using var first = SingleInstance.Claim(slot);

        Assert.NotNull(first);
        Assert.Null(SingleInstance.Claim(slot));
    }

    /// <summary>
    /// The hand-over an accepted update depends on. Without it the replacement would find the
    /// slot still held by the copy that launched it and exit — an update that looks like d47
    /// simply quitting.
    /// </summary>
    [Fact]
    public void ReleasingForASuccessorFreesTheSlotImmediately()
    {
        var slot = Slot();

        var outgoing = SingleInstance.Claim(slot);
        Assert.NotNull(outgoing);

        Assert.Null(SingleInstance.Claim(slot));

        outgoing.ReleaseForSuccessor();

        using var successor = SingleInstance.Claim(slot);
        Assert.NotNull(successor);
    }

    /// <summary>Ordinary shutdown frees it too, and doing both is not an error.</summary>
    [Fact]
    public void TheSlotComesBackAfterDisposalAndDisposingTwiceIsFine()
    {
        var slot = Slot();

        var only = SingleInstance.Claim(slot);
        Assert.NotNull(only);

        only.Dispose();
        only.Dispose();

        using var next = SingleInstance.Claim(slot);
        Assert.NotNull(next);
    }
}
