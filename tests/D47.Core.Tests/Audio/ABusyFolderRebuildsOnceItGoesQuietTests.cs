using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class ABusyFolderRebuildsOnceItGoesQuietTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(3);

    [Fact]
    public void SeveralChangesInsideTheWindowGiveOneRebuild()
    {
        var quiet = new QuietPeriod(Quiet);

        for (var i = 0; i < 20; i++)
        {
            quiet.Changed(T0 + TimeSpan.FromMilliseconds(100 * i));
            Assert.False(quiet.Start(T0 + TimeSpan.FromMilliseconds(100 * i + 50)));
        }

        var last = T0 + TimeSpan.FromMilliseconds(1900);

        Assert.Equal(Quiet, quiet.Wait(last));
        Assert.False(quiet.Start(last + Quiet - TimeSpan.FromMilliseconds(1)));
        Assert.True(quiet.Start(last + Quiet));
        quiet.Finished();

        Assert.False(quiet.Start(last + Quiet + Quiet));
        Assert.Null(quiet.Wait(last + Quiet + Quiet));
    }

    [Fact]
    public void AChangeAfterTheWindowGivesASecondRebuild()
    {
        var quiet = new QuietPeriod(Quiet);

        quiet.Changed(T0);
        Assert.True(quiet.Start(T0 + Quiet));
        quiet.Finished();

        var later = T0 + TimeSpan.FromSeconds(10);
        quiet.Changed(later);

        Assert.False(quiet.Start(later + TimeSpan.FromSeconds(1)));
        Assert.True(quiet.Start(later + Quiet));
    }

    [Fact]
    public void AChangeDuringARebuildWaitsForItAndGivesOneMore()
    {
        var quiet = new QuietPeriod(Quiet);

        quiet.Changed(T0);
        Assert.True(quiet.Start(T0 + Quiet));

        var during = T0 + TimeSpan.FromSeconds(4);
        quiet.Changed(during);
        quiet.Changed(during + TimeSpan.FromSeconds(1));

        // Quiet long enough, but the first is still running.
        Assert.False(quiet.Start(during + TimeSpan.FromSeconds(10)));
        Assert.Null(quiet.Wait(during + TimeSpan.FromSeconds(10)));

        quiet.Finished();

        Assert.Equal(TimeSpan.Zero, quiet.Wait(during + TimeSpan.FromSeconds(10)));
        Assert.True(quiet.Start(during + TimeSpan.FromSeconds(10)));
        quiet.Finished();

        Assert.False(quiet.Pending);
    }
}
