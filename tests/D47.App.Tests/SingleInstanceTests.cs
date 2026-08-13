using D47.App;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// One d47 per Commander. A second copy is a second journal tailer, a second microphone, a
/// second set of global hotkeys and two writers over one data folder — none of which is visible
/// until something behaves oddly.
/// </summary>
[Collection("single-instance")]
public class SingleInstanceTests
{
    [Fact]
    public void TheSecondCopyDoesNotGetTheSlot()
    {
        using var first = SingleInstance.Claim();

        Assert.NotNull(first);
        Assert.Null(SingleInstance.Claim());
    }

    /// <summary>
    /// The hand-over an accepted update depends on. Without it the replacement would find the
    /// slot still held by the copy that launched it and exit — an update that looks like d47
    /// simply quitting.
    /// </summary>
    [Fact]
    public void ReleasingForASuccessorFreesTheSlotImmediately()
    {
        var outgoing = SingleInstance.Claim();
        Assert.NotNull(outgoing);

        Assert.Null(SingleInstance.Claim());

        outgoing.ReleaseForSuccessor();

        using var successor = SingleInstance.Claim();
        Assert.NotNull(successor);
    }

    /// <summary>Ordinary shutdown frees it too, and doing both is not an error.</summary>
    [Fact]
    public void TheSlotComesBackAfterDisposalAndDisposingTwiceIsFine()
    {
        var only = SingleInstance.Claim();
        Assert.NotNull(only);

        only.Dispose();
        only.Dispose();

        using var next = SingleInstance.Claim();
        Assert.NotNull(next);
    }
}

/// <summary>
/// The slot is process-wide, so these cannot run beside each other or they would each see the
/// other's claim and call it a failure.
/// </summary>
[CollectionDefinition("single-instance", DisableParallelization = true)]
public class SingleInstanceCollection;
