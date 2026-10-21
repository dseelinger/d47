using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>The crop square's arithmetic over integers: the start, a move, a corner resize, the clamp and the 384 px floor.</summary>
public class ACropSquareStaysSquareAndInsideThePictureTests
{
    private const int W = 3840;

    private const int H = 2160;

    [Fact]
    public void TheStartIsTheLargestCentredSquare()
    {
        Assert.Equal(new CropSquare(840, 0, 2160), CropSquare.Start(W, H));
        Assert.Equal(new CropSquare(0, 120, 2016), CropSquare.Start(2016, 2256));
    }

    [Fact]
    public void AMoveFollowsTheDragAndStopsAtTheEdges()
    {
        var square = new CropSquare(100, 100, 500);

        Assert.Equal(new CropSquare(150, 70, 500), square.Move(50, -30, W, H));
        Assert.Equal(new CropSquare(0, 0, 500), square.Move(-5000, -5000, W, H));
        Assert.Equal(new CropSquare(W - 500, H - 500, 500), square.Move(9000, 9000, W, H));
    }

    [Fact]
    public void ABottomRightResizeKeepsTheTopLeftCornerAndStaysSquare()
    {
        var grown = new CropSquare(100, 100, 500).Resize(CropCorner.BottomRight, 100, 100, W, H);

        Assert.Equal(new CropSquare(100, 100, 600), grown);
    }

    [Fact]
    public void ATopLeftResizeKeepsTheBottomRightCorner()
    {
        var grown = new CropSquare(300, 300, 500).Resize(CropCorner.TopLeft, -100, -100, W, H);

        Assert.Equal(new CropSquare(200, 200, 600), grown);
        Assert.Equal(800, grown.X + grown.Side);
        Assert.Equal(800, grown.Y + grown.Side);
    }

    [Fact]
    public void AResizeStopsAtThePictureEdge()
    {
        var grown = new CropSquare(3000, 100, 500).Resize(CropCorner.BottomRight, 5000, 5000, W, H);

        Assert.Equal(new CropSquare(3000, 100, 840), grown);
    }

    [Fact]
    public void AResizeNeverGoesBelowTheFloor()
    {
        var shrunk = new CropSquare(100, 100, 500).Resize(CropCorner.BottomRight, -5000, -5000, W, H);
        var other = new CropSquare(100, 100, 500).Resize(CropCorner.TopLeft, 5000, 5000, W, H);

        Assert.Equal(CropSquare.Floor, shrunk.Side);
        Assert.Equal(CropSquare.Floor, other.Side);
        Assert.Equal(600, other.X + other.Side);
    }

    [Fact]
    public void APictureSmallerThanTheFloorKeepsTheWholePicture()
    {
        var start = CropSquare.Start(300, 200);

        Assert.Equal(200, start.Side);
        Assert.Equal(200, start.Resize(CropCorner.BottomRight, -100, -100, 300, 200).Side);
    }

    [Fact]
    public void ASavedSquareIsFittedToThePictureItIsOpenedOn()
    {
        Assert.Equal(new CropSquare(1000, 0, 1000), new CropSquare(3000, 900, 5000).Fit(2000, 1000));
        Assert.Equal(CropSquare.Floor, new CropSquare(0, 0, 10).Fit(W, H).Side);
    }

    [Fact]
    public void ASquareWritesAndReadsBack()
    {
        var square = new CropSquare(12, 34, 456);

        Assert.Equal(square, CropSquare.Parse(square.Write()));
        Assert.Null(CropSquare.Parse("not json"));
        Assert.Null(CropSquare.Parse("{}"));
    }

    [Fact]
    public void TheKeptFilesAreNamedBesideThePicture()
    {
        var key = new CodexPictureKey(2420101, 18);

        Assert.Equal("2420101-18.source.jpg", key.SourceFileName);
        Assert.Equal("2420101-18.crop.json", key.CropFileName);
        Assert.False(CodexPictureKey.TryParse(key.SourceFileName, out _));
        Assert.False(CodexPictureKey.TryParse(key.CropFileName, out _));
    }
}
