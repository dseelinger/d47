namespace D47.Core.Interface;

/// <summary>Reads a JPEG's pixel size from its header, without decoding the picture.</summary>
public static class JpegSize
{
    /// <summary>The width and height from the first frame header in <paramref name="jpeg"/>; null when it is not a JPEG or has none.</summary>
    public static (int Width, int Height)? TryRead(Stream jpeg)
    {
        ArgumentNullException.ThrowIfNull(jpeg);

        if (jpeg.ReadByte() != 0xFF || jpeg.ReadByte() != 0xD8)
        {
            return null;
        }

        while (true)
        {
            int marker;

            do
            {
                marker = jpeg.ReadByte();

                if (marker < 0)
                {
                    return null;
                }
            }
            while (marker != 0xFF);

            int code;

            do
            {
                code = jpeg.ReadByte();
            }
            while (code == 0xFF);

            if (code < 0 || code == 0xD9 || code == 0xDA)
            {
                return null;
            }

            if (code == 0x01 || code is >= 0xD0 and <= 0xD8)
            {
                continue;
            }

            var high = jpeg.ReadByte();
            var low = jpeg.ReadByte();

            if (high < 0 || low < 0)
            {
                return null;
            }

            var length = (high << 8) | low;

            if (IsFrame(code))
            {
                var frame = new byte[5];

                if (length < 7 || jpeg.Read(frame, 0, 5) < 5)
                {
                    return null;
                }

                var height = (frame[1] << 8) | frame[2];
                var width = (frame[3] << 8) | frame[4];

                return width > 0 && height > 0 ? (width, height) : null;
            }

            for (var skipped = 0; skipped < length - 2; skipped++)
            {
                if (jpeg.ReadByte() < 0)
                {
                    return null;
                }
            }
        }
    }

    private static bool IsFrame(int code) => code is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
}
