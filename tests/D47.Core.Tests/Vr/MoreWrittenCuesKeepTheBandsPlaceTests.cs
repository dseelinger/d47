using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>Cues written from a soundtrack arrive a stretch at a time; each arrival keeps the band's place (#2677).</summary>
public class MoreWrittenCuesKeepTheBandsPlaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 21, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private static readonly SubtitleCue First = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), "Liftoff of the launch vehicle.");

    private static readonly SubtitleCue Later = new(TimeSpan.FromSeconds(26), TimeSpan.FromSeconds(29), "The tower is clear.");

    [Fact]
    public void ACueWrittenForThePositionIsShownAtOnce()
    {
        var layer = new CaptionLayer();
        layer.Follow([First]);
        layer.At(TimeSpan.FromSeconds(27));

        layer.Extend([First, Later]);

        Assert.Equal(Caption.Wrap(Later.Text), layer.Lines);
    }

    [Fact]
    public void TheCueShownStaysWithoutAChangeWhenMoreAreWrittenElsewhere()
    {
        var layer = new CaptionLayer();
        layer.Follow([First]);
        layer.At(TimeSpan.FromSeconds(2));
        var changes = 0;
        layer.Changed += () => changes++;

        layer.Extend([First, Later]);

        Assert.Equal(0, changes);
        Assert.Equal(Caption.Wrap(First.Text), layer.Lines);
    }

    [Fact]
    public void D47sFinishedLineStillGivesWayWhenMoreCuesArriveDuringIt()
    {
        var layer = new CaptionLayer();
        layer.Follow([First]);
        layer.At(TimeSpan.FromSeconds(2));
        layer.Say("Fuel is at nineteen per cent.", Now);
        layer.Quiet(Now + (Second * 2));

        layer.Extend([First, Later]);
        layer.At(TimeSpan.FromSeconds(2.1));

        Assert.Equal(Caption.Wrap(First.Text), layer.Lines);
    }
}
