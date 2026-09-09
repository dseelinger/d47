using D47.App;
using Xunit;

namespace D47.App.Tests;

/// <summary>One d47 per Commander.</summary>
public class SingleInstanceTests
{
    /// <summary>A slot of this test's own.</summary>
    private static string Slot() => @"Local\" + $"d47-tests-{Guid.NewGuid():N}";

    [Fact]
    public void TheSecondCopyDoesNotGetTheSlot()
    {
        var slot = Slot();

        using var first = SingleInstance.Claim(slot);

        Assert.NotNull(first);
        Assert.Null(SingleInstance.Claim(slot));
    }

    /// <summary>The hand-over an accepted update depends on.</summary>
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
