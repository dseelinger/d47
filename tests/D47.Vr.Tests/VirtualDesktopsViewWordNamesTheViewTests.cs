using System.Buffers.Binary;
using System.Diagnostics;
using D47.Core.Vr;
using Xunit;

namespace D47.Vr.Tests;

public class VirtualDesktopsViewWordNamesTheViewTests
{
    private const long Now = 1_000_000_000_000;

    [Fact]
    public void ZeroWithAFreshStampIsTheDesktop() =>
        Assert.Equal(HeadsetView.Desktop, VirtualDesktopView.Decode(Block(view: 0, stamp: Now), Now));

    [Fact]
    public void OneWithAFreshStampIsVr() =>
        Assert.Equal(HeadsetView.Vr, VirtualDesktopView.Decode(Block(view: 1, stamp: Now), Now));

    [Fact]
    public void AnyOtherValueIsUnknown() =>
        Assert.Equal(HeadsetView.Unknown, VirtualDesktopView.Decode(Block(view: 2, stamp: Now), Now));

    [Fact]
    public void AStampHalfASecondOldIsStillTrusted() =>
        Assert.Equal(HeadsetView.Vr, VirtualDesktopView.Decode(Block(view: 1, stamp: Now - (Stopwatch.Frequency / 2)), Now));

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    public void AStampTwoSecondsOldIsUnknownWhateverTheViewSays(uint view) =>
        Assert.Equal(HeadsetView.Unknown, VirtualDesktopView.Decode(Block(view, stamp: Now - (2 * Stopwatch.Frequency)), Now));

    [Fact]
    public void AStampLaterThanTheReadIsUnknown() =>
        Assert.Equal(HeadsetView.Unknown, VirtualDesktopView.Decode(Block(view: 1, stamp: Now + 1), Now));

    [Fact]
    public void ABlockShorterThanTheViewWordIsUnknown() =>
        Assert.Equal(HeadsetView.Unknown, VirtualDesktopView.Decode(Block(view: 1, stamp: Now).AsSpan(0, 0x207), Now));

    private static byte[] Block(uint view, long stamp)
    {
        var block = new byte[4096];
        BinaryPrimitives.WriteInt64LittleEndian(block.AsSpan(0xF8), stamp);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(0x204), view);
        return block;
    }
}
