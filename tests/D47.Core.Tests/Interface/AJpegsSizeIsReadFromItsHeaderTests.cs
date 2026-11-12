using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class AJpegsSizeIsReadFromItsHeaderTests
{
    private static byte[] Jpeg(int width, int height) =>
    [
        0xFF, 0xD8,
        0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00,
        0xFF, 0xC0, 0x00, 0x0B, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x01, 0x01, 0x11, 0x00,
        0xFF, 0xD9,
    ];

    [Fact]
    public void TheFrameHeaderGivesTheSize()
    {
        Assert.Equal((12288, 13056), JpegSize.TryRead(new MemoryStream(Jpeg(12288, 13056))));
        Assert.Equal((3840, 2160), JpegSize.TryRead(new MemoryStream(Jpeg(3840, 2160))));
    }

    [Fact]
    public void ABytesThatIsNotAJpegHasNoSize()
    {
        Assert.Null(JpegSize.TryRead(new MemoryStream([1, 2, 3, 4])));
        Assert.Null(JpegSize.TryRead(new MemoryStream([])));
    }

    [Fact]
    public void AJpegCutOffBeforeItsFrameHeaderHasNoSize()
    {
        Assert.Null(JpegSize.TryRead(new MemoryStream(Jpeg(100, 50)[..12])));
    }
}
