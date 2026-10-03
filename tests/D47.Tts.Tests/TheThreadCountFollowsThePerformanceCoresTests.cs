using Xunit;

namespace D47.Tts.Tests;

public class TheThreadCountFollowsThePerformanceCoresTests
{
    private static byte[] Cores(int count, byte efficiencyClass) => Enumerable.Repeat(efficiencyClass, count).ToArray();

    [Fact]
    public void EightPerformanceAndSixteenEfficiencyCoresGiveEight() =>
        Assert.Equal(8, PerformanceCores.ThreadsFor([.. Cores(8, 1), .. Cores(16, 0)]));

    [Fact]
    public void SixCoresOfOneClassGiveSix() =>
        Assert.Equal(6, PerformanceCores.ThreadsFor(Cores(6, 0)));

    [Fact]
    public void TwelveCoresOfOneClassAreCappedAtEight() =>
        Assert.Equal(8, PerformanceCores.ThreadsFor(Cores(12, 0)));

    [Fact]
    public void SixPerformanceCoresBesideEightEfficiencyCoresGiveSix() =>
        Assert.Equal(6, PerformanceCores.ThreadsFor([.. Cores(8, 0), .. Cores(6, 1)]));

    [Fact]
    public void ThisMachineGetsBetweenOneAndEight() =>
        Assert.InRange(PerformanceCores.ForThisMachine(), 1, PerformanceCores.Cap);
}
