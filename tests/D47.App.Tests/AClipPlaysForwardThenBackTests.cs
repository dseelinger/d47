using Xunit;
using D47.App.Panel;

namespace D47.App.Tests;

public class AClipPlaysForwardThenBackTests
{
    private static List<int> Shown(int count, int ticks)
    {
        var shown = new List<int>();
        var (index, direction) = (0, 1);

        for (var i = 0; i < ticks; i++)
        {
            (index, direction) = AvatarView.Step(index, direction, count);
            shown.Add(index);
        }

        return shown;
    }

    [Fact]
    public void AClipPlaysForwardToItsLastFrameThenBackToItsFirst()
    {
        Assert.Equal([1, 2, 3, 4, 3, 2, 1, 0, 1], Shown(count: 5, ticks: 9));
    }

    [Fact]
    public void NeitherEndFrameIsShownTwiceInARow()
    {
        var shown = Shown(count: 6, ticks: 60);

        Assert.DoesNotContain(
            shown.Zip(shown.Skip(1)),
            pair => pair.First == pair.Second);
    }

    [Fact]
    public void ATwoFrameClipAlternatesItsFrames()
    {
        Assert.Equal([1, 0, 1, 0], Shown(count: 2, ticks: 4));
    }

    [Fact]
    public void AOneFrameClipStaysOnItsFrame()
    {
        Assert.Equal([0, 0, 0], Shown(count: 1, ticks: 3));
    }
}
