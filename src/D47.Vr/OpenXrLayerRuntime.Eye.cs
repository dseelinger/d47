using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using D47.Core.Vr;
using D47.XrLayer;
using Microsoft.Extensions.Logging;

namespace D47.Vr;

/// <summary>A picture of the headset's left eye with D47's quads in it, as opaque BGRA rows, or why there is none.</summary>
public sealed record HeadsetEye(byte[]? Pixels, int Width, int Height, string? Refusal)
{
    public static HeadsetEye Refused(string why) => new(null, 0, 0, why);
}

public sealed unsafe partial class OpenXrLayerRuntime
{
    /// <summary>How long a picture waits for the layer to copy Elite's eye.</summary>
    public static readonly TimeSpan EyeLimit = TimeSpan.FromSeconds(1);

    /// <summary>Frames the layer draws after its answer before the copy is read, since the two devices share no fence.</summary>
    internal const ulong FramesBeforeReading = 2;

    public const string NoEyeReader = "D47 has no way to read the headset's eye here.";

    public const string NotServing = "The headset is not shown through the D47 layer.";

    public const string NoEyeYet = "Elite has not shown an eye image through the D47 layer yet.";

    public const string EyeTimedOut = "Elite did not hand over its eye image within a second.";

    public const string EyeSessionEnded = "The headset session ended before the picture was taken.";

    /// <summary>The slots in the order the layer draws them, so later ones cover earlier ones.</summary>
    private static readonly int[] DrawOrder = [PanelSlot, AvatarSlot, VideoSlot, CaptionSlot, BeamSlot, CursorSlot];

    private readonly Lock _eyeGate = new();
    private ulong _eyeSerial = (ulong)DateTime.UtcNow.Ticks;
    private bool _eyeLogged;

    /// <summary>
    /// Asks the layer for Elite's left eye and draws D47's quads over it. Blocks for up to <see cref="EyeLimit"/>, so
    /// never call it on the tick or the serve thread.
    /// </summary>
    public HeadsetEye TakeEye(CancellationToken cancellationToken)
    {
        lock (_eyeGate)
        {
            if (BeginEye(out var pending) is { } refused)
            {
                return HeadsetEye.Refused(refused);
            }

            try
            {
                var clock = Stopwatch.StartNew();

                while (true)
                {
                    if (PollEye(pending!) is { } taken)
                    {
                        return taken;
                    }

                    if (clock.Elapsed >= EyeLimit)
                    {
                        return HeadsetEye.Refused(EyeTimedOut);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(5);
                }
            }
            finally
            {
                eyes!.Release(pending!.Texture);
            }
        }
    }

    /// <summary>A refusal, as the reason a picture gives.</summary>
    internal static string Describe(XrCaptureRefusal refusal) => refusal switch
    {
        XrCaptureRefusal.NoEyeImage => "Elite's eye image could not be read through the D47 layer.",
        XrCaptureRefusal.TextureNotOpened => "The D47 layer could not open the picture's texture.",
        XrCaptureRefusal.TextureMismatch => "Elite's eye image changed size or format while the picture was taken.",
        XrCaptureRefusal.Multisampled => "Elite's eye image is multisampled, which a picture cannot read.",
        XrCaptureRefusal.NotReleased => "Elite did not hand over its eye image.",
        XrCaptureRefusal.NoPose => "The D47 layer could not tell where the eye was.",
        XrCaptureRefusal.Faulted => "The D47 layer faulted copying Elite's eye image. Restart Elite.",
        _ => $"The D47 layer refused the picture ({refusal}).",
    };

    /// <summary>
    /// Makes the texture, notes what each visible quad shows, and raises the request. Null with
    /// <paramref name="pending"/> set when asked, otherwise why not.
    /// </summary>
    internal string? BeginEye(out PendingEye? pending)
    {
        pending = null;

        if (eyes is null)
        {
            return NoEyeReader;
        }

        XrLayerHalf layer;

        lock (_half)
        {
            if (_block is null)
            {
                return NotServing;
            }

            if (XrSeqLock.TryRead(&_block->Layer, out var read))
            {
                _layer = read;
            }

            layer = _layer;
        }

        if (layer.EyeWidth == 0 || layer.EyeHeight == 0)
        {
            return NoEyeYet;
        }

        if (!EyeFormats.Readable(layer.EyeFormat))
        {
            return $"Elite's eye image is DXGI format {layer.EyeFormat}, which a picture cannot read.";
        }

        if (!_eyeLogged)
        {
            _eyeLogged = true;
            logger.LogInformation(
                "Elite's left eye through the D47 layer is {Width}x{Height}, DXGI format {Format}",
                layer.EyeWidth,
                layer.EyeHeight,
                layer.EyeFormat);
        }

        if (eyes.OpenOnLuid(layer.AdapterLuidLow, layer.AdapterLuidHigh) is { } unopened)
        {
            return unopened;
        }

        var name = NextName();
        var texture = eyes.CreateShared((int)layer.EyeWidth, (int)layer.EyeHeight, layer.EyeFormat, name, out var refused);

        if (refused is not null)
        {
            return refused;
        }

        var shown = ShownQuads();

        lock (_half)
        {
            if (_block is null)
            {
                eyes.Release(texture);
                return NotServing;
            }

            var serial = ++_eyeSerial;
            _d47.CaptureRequest = serial;
            Span<char> characters = _d47.CaptureTexture;
            characters.Clear();
            name.AsSpan(0, Math.Min(name.Length, XrBlock.TextureNameLength - 1)).CopyTo(characters);
            Publish();

            pending = new PendingEye(serial, texture, (int)layer.EyeWidth, (int)layer.EyeHeight, shown, _curved);
        }

        return null;
    }

    /// <summary>The picture once the layer has answered and drawn enough frames since, otherwise null.</summary>
    internal HeadsetEye? PollEye(PendingEye pending)
    {
        XrLayerHalf layer;

        lock (_half)
        {
            if (_block is null)
            {
                return HeadsetEye.Refused(EyeSessionEnded);
            }

            if (XrSeqLock.TryRead(&_block->Layer, out var read))
            {
                _layer = read;
            }

            layer = _layer;
        }

        if (layer.CaptureServed != pending.Serial)
        {
            return null;
        }

        if (layer.CaptureRefusal != XrCaptureRefusal.None)
        {
            return HeadsetEye.Refused(Describe(layer.CaptureRefusal));
        }

        if (layer.FrameCounter < layer.CaptureFrame + FramesBeforeReading)
        {
            return null;
        }

        if (eyes!.Read(pending.Texture, out var unread) is not { } pixels)
        {
            return HeadsetEye.Refused($"The headset's eye could not be read: {unread}");
        }

        if (pixels.Length < pending.Width * pending.Height * 4)
        {
            return HeadsetEye.Refused("The headset's eye came back smaller than it was asked for.");
        }

        var view = new EyeView(
            ToPose(layer.CaptureEye),
            layer.CaptureFov.AngleLeft,
            layer.CaptureFov.AngleRight,
            layer.CaptureFov.AngleUp,
            layer.CaptureFov.AngleDown);
        var head = (layer.CaptureHeadValid & 0x3) == 0x3 && ToPose(layer.CaptureHead) is { IsFinite: true } located
            ? located
            : (VrPose?)null;

        EyeComposite.Draw(pixels, pending.Width, pending.Height, view, Place(pending, head));
        return new HeadsetEye(pixels, pending.Width, pending.Height, null);
    }

    /// <summary>The noted quads in the LOCAL space; ones on the head are left out when the head was not located.</summary>
    private static List<EyeQuad> Place(PendingEye pending, VrPose? head)
    {
        var placed = new List<EyeQuad>();

        foreach (var quad in pending.Quads)
        {
            var pose = ToPose(quad.Slot.Pose);

            if (quad.Slot.Space == XrQuadSpace.Head)
            {
                if (head is not { } on)
                {
                    continue;
                }

                pose = new VrPose(
                    on.Position + Vector3.Transform(pose.Position, on.Facing),
                    on.Facing * pose.Facing);
            }

            var slot = quad.Slot;
            var curvature = pending.Curved && quad.Index is PanelSlot or AvatarSlot or VideoSlot ? slot.Curvature : 0f;

            placed.Add(new EyeQuad(
                pose,
                slot.WidthMetres,
                slot.WidthMetres * slot.RectHeight / slot.RectWidth,
                curvature,
                quad.Pixels,
                slot.RectWidth,
                slot.RectHeight));
        }

        return placed;
    }

    /// <summary>Each visible slot, in draw order, with a copy of the picture it shows.</summary>
    private List<ShownQuad> ShownQuads()
    {
        XrQuadSlot[] slots;
        Sprite? beam;
        Sprite? cursor;
        float videoOpacity;

        lock (_half)
        {
            slots = new XrQuadSlot[XrBlock.SlotCount];

            for (var i = 0; i < XrBlock.SlotCount; i++)
            {
                slots[i] = _d47.Slots[i];
            }

            beam = _beam;
            cursor = _cursorShown;
            videoOpacity = _videoOpacity;
        }

        var shown = new List<ShownQuad>();

        foreach (var index in DrawOrder)
        {
            var slot = slots[index];

            if (slot.Visible != 1 || slot.RectWidth <= 0 || slot.RectHeight <= 0 || !(slot.WidthMetres > 0))
            {
                continue;
            }

            var pixels = index switch
            {
                BeamSlot => Sized(beam?.Bgra, beam?.Width ?? 0, slot),
                CursorSlot => Sized(cursor?.Bgra, cursor?.Width ?? 0, slot),
                VideoSlot => VideoPixels(slot, videoOpacity),
                _ => BufferPixels(index, slot),
            };

            if (pixels is not null)
            {
                shown.Add(new ShownQuad(index, slot, pixels));
            }
        }

        return shown;
    }

    /// <summary>The slot's rectangle of a picture <paramref name="width"/> wide; null when the picture is smaller.</summary>
    private static byte[]? Sized(byte[]? picture, int width, XrQuadSlot slot)
    {
        if (picture is null || width < slot.RectWidth || picture.Length < width * slot.RectHeight * 4)
        {
            return null;
        }

        var rows = new byte[slot.RectWidth * slot.RectHeight * 4];

        for (var row = 0; row < slot.RectHeight; row++)
        {
            Buffer.BlockCopy(picture, row * width * 4, rows, row * slot.RectWidth * 4, slot.RectWidth * 4);
        }

        return rows;
    }

    private byte[]? BufferPixels(int index, XrQuadSlot slot)
    {
        lock (_drawing)
        {
            if (!_buffers.TryGetValue(index, out var buffer)
                || buffer.Width < slot.RectWidth
                || buffer.Height < slot.RectHeight)
            {
                return null;
            }

            var rows = new byte[slot.RectWidth * slot.RectHeight * 4];

            for (var row = 0; row < slot.RectHeight; row++)
            {
                Marshal.Copy(buffer.Address + (row * (nint)buffer.RowBytes), rows, row * slot.RectWidth * 4, slot.RectWidth * 4);
            }

            return rows;
        }
    }

    private byte[]? VideoPixels(XrQuadSlot slot, float opacity)
    {
        if (video?.Picture is not { } picture
            || picture.Width != slot.RectWidth
            || picture.Height != slot.RectHeight
            || picture.Pixels.Length < picture.Width * picture.Height * 4)
        {
            return null;
        }

        var rows = new byte[picture.Width * picture.Height * 4];
        VrVideoCopy.Opaque(picture.Pixels.AsSpan(0, rows.Length), rows, opacity);
        return rows;
    }

    /// <summary>A request the layer has not yet answered, and what D47 showed when it was made.</summary>
    internal sealed record PendingEye(ulong Serial, IntPtr Texture, int Width, int Height, IReadOnlyList<ShownQuad> Quads, bool Curved);

    /// <summary>A slot as it was when a picture was asked for, with the picture it showed.</summary>
    internal sealed record ShownQuad(int Index, XrQuadSlot Slot, byte[] Pixels);
}
