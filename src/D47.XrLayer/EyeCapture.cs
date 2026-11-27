using System.Numerics;

namespace D47.XrLayer;

/// <summary>
/// Copies Elite's left eye into a texture d47 names, when d47 raises <see cref="XrD47Half.CaptureRequest"/>. The eye
/// is the first view of Elite's projection layer. The copy is made as Elite releases that view's swapchain image, and
/// answered at the same frame's xrEndFrame with the eye's pose in the LOCAL space and its field of view. Never throws:
/// a fault turns capturing off for the session and answers the request in hand.
/// </summary>
internal sealed unsafe class EyeCapture(ILayerCalls calls)
{
    private const int MostSwapchains = 8;
    private const int MostImages = 16;

    private readonly Swapchain[] known = [new(), new(), new(), new(), new(), new(), new(), new()];
    private int nextKnown;

    private Eye eye;
    private bool faulted;
    private ulong handled;
    private ulong pending;
    private nint destination;
    private bool copied;
    private int framesWaited;

    /// <summary>Notes the image Elite acquired from one of its swapchains.</summary>
    public void Acquired(ulong swapchain, uint index)
    {
        if (faulted)
        {
            return;
        }

        var entry = Find(swapchain);
        if (entry is null)
        {
            entry = known[nextKnown];
            nextKnown = (nextKnown + 1) % MostSwapchains;
            entry.Forget(swapchain);
        }

        entry.LastAcquired = (int)index;
    }

    /// <summary>Copies the eye image a request waits for, just before Elite releases it.</summary>
    public void BeforeRelease(ulong swapchain, ITextureDevice? device)
    {
        if (faulted || pending == 0 || copied || device is null || !eye.Valid || swapchain != eye.Swapchain)
        {
            return;
        }

        try
        {
            var entry = Find(swapchain);
            if (entry is null || !entry.Described || entry.LastAcquired < 0 || entry.LastAcquired >= entry.Images.Length)
            {
                return;
            }

            device.CopyRegion(
                entry.Images[entry.LastAcquired],
                eye.ArrayIndex * entry.Description.MipLevels,
                eye.Rect,
                destination);
            device.Flush();
            copied = true;
        }
        catch (Exception)
        {
            faulted = true;
        }
    }

    /// <summary>
    /// Reads the frame's eye, publishes its format and size, and moves a request on: started, answered once its copy
    /// is made, or refused. Call after the head is located for <paramref name="time"/>.
    /// </summary>
    public void Frame(
        XrFrameEndInfo* frameEndInfo,
        long time,
        in XrD47Half d47,
        ref XrLayerHalf published,
        ITextureDevice device,
        ulong localSpace)
    {
        if (faulted)
        {
            if (pending == 0 && d47.CaptureRequest != 0 && d47.CaptureRequest != handled)
            {
                handled = d47.CaptureRequest;
                pending = handled;
            }

            Answer(XrCaptureRefusal.Faulted, ref published, device);
            published.EyeFormat = 0;
            published.EyeWidth = 0;
            published.EyeHeight = 0;
            return;
        }

        try
        {
            ReadEye(frameEndInfo, device);
            var entry = eye.Valid ? Find(eye.Swapchain) : null;
            published.EyeFormat = entry is not null ? entry.Description.Format : 0;
            published.EyeWidth = entry is not null ? (uint)eye.Rect.Width : 0;
            published.EyeHeight = entry is not null ? (uint)eye.Rect.Height : 0;

            if (pending != 0)
            {
                if (copied)
                {
                    Serve(time, ref published, device, localSpace);
                }
                else if (++framesWaited > EyeCaptureLimits.MostFramesWaited)
                {
                    Answer(XrCaptureRefusal.NotReleased, ref published, device);
                }

                return;
            }

            var request = d47.CaptureRequest;
            if (request != 0 && request != handled)
            {
                Begin(request, in d47, ref published, device, entry);
            }
        }
        catch (Exception)
        {
            faulted = true;
            Answer(XrCaptureRefusal.Faulted, ref published, device);
        }
    }

    /// <summary>Releases the texture a request holds. Called before the session is destroyed.</summary>
    public void Destroy(ITextureDevice? device)
    {
        if (destination != 0)
        {
            device?.Release(destination);
            destination = 0;
        }

        pending = 0;
    }

    /// <summary>The pose of <paramref name="child"/>, given in <paramref name="parent"/>'s space, in the parent's own space.</summary>
    internal static XrBlockPose Compose(in XrPosef parent, in XrPosef child)
    {
        var turn = new Quaternion(parent.OrientationX, parent.OrientationY, parent.OrientationZ, parent.OrientationW);
        var position = Vector3.Transform(new Vector3(child.PositionX, child.PositionY, child.PositionZ), turn)
            + new Vector3(parent.PositionX, parent.PositionY, parent.PositionZ);
        var facing = turn * new Quaternion(child.OrientationX, child.OrientationY, child.OrientationZ, child.OrientationW);
        return new XrBlockPose
        {
            OrientationX = facing.X,
            OrientationY = facing.Y,
            OrientationZ = facing.Z,
            OrientationW = facing.W,
            PositionX = position.X,
            PositionY = position.Y,
            PositionZ = position.Z,
        };
    }

    private void Begin(ulong request, in XrD47Half d47, ref XrLayerHalf published, ITextureDevice device, Swapchain? entry)
    {
        handled = request;
        pending = request;
        copied = false;
        framesWaited = 0;

        if (entry is null)
        {
            Answer(XrCaptureRefusal.NoEyeImage, ref published, device);
            return;
        }

        if (entry.Description.SampleCount != 1)
        {
            Answer(XrCaptureRefusal.Multisampled, ref published, device);
            return;
        }

        ReadOnlySpan<char> name = d47.CaptureTexture;
        var end = name.IndexOf('\0');
        if (end <= 0)
        {
            Answer(XrCaptureRefusal.TextureNotOpened, ref published, device);
            return;
        }

        var terminated = stackalloc char[end + 1];
        name[..end].CopyTo(new Span<char>(terminated, end));
        terminated[end] = '\0';
        destination = device.OpenSharedWritable(terminated);
        if (destination == 0)
        {
            Answer(XrCaptureRefusal.TextureNotOpened, ref published, device);
            return;
        }

        var description = device.Describe(destination);
        if (description.Format != entry.Description.Format
            || description.Width != (uint)eye.Rect.Width
            || description.Height != (uint)eye.Rect.Height
            || description.SampleCount != 1
            || description.ArraySize != 1)
        {
            Answer(XrCaptureRefusal.TextureMismatch, ref published, device);
        }
    }

    private void Serve(long time, ref XrLayerHalf published, ITextureDevice device, ulong localSpace)
    {
        var location = new XrSpaceLocation { Type = XrType.SpaceLocation };
        const ulong Located = XrConstant.LocationOrientationValid | XrConstant.LocationPositionValid;
        if (calls.LocateSpace(eye.Space, localSpace, time, &location) < XrResult.Success
            || (location.LocationFlags & Located) != Located)
        {
            Answer(XrCaptureRefusal.NoPose, ref published, device);
            return;
        }

        published.CaptureEye = Compose(location.Pose, eye.Pose);
        published.CaptureFov = new XrBlockFov
        {
            AngleLeft = eye.Fov.AngleLeft,
            AngleRight = eye.Fov.AngleRight,
            AngleUp = eye.Fov.AngleUp,
            AngleDown = eye.Fov.AngleDown,
        };
        published.CaptureHead = published.Head;
        published.CaptureHeadValid = published.HeadValid;
        Answer(XrCaptureRefusal.None, ref published, device);
    }

    /// <summary>Answers the request in hand, if there is one, and lets go of its texture.</summary>
    private void Answer(XrCaptureRefusal refusal, ref XrLayerHalf published, ITextureDevice? device)
    {
        if (pending == 0)
        {
            return;
        }

        published.CaptureRefusal = refusal;
        published.CaptureFrame = published.FrameCounter;
        published.CaptureServed = pending;
        pending = 0;
        copied = false;

        if (destination != 0)
        {
            try
            {
                device?.Release(destination);
            }
            catch (Exception)
            {
                faulted = true;
            }

            destination = 0;
        }
    }

    /// <summary>The first view of Elite's first projection layer, with its swapchain's images listed once.</summary>
    private void ReadEye(XrFrameEndInfo* frameEndInfo, ITextureDevice device)
    {
        eye = default;
        if (frameEndInfo is null || frameEndInfo->Layers is null)
        {
            return;
        }

        for (var i = 0; i < frameEndInfo->LayerCount && i < XrConstant.MinLayersSupported; i++)
        {
            var layer = frameEndInfo->Layers[i];
            if (layer is null || layer->Type != XrType.CompositionLayerProjection)
            {
                continue;
            }

            var projection = (XrCompositionLayerProjection*)layer;
            if (projection->ViewCount < 1 || projection->Views is null
                || projection->Views[0].Type != XrType.CompositionLayerProjectionView)
            {
                return;
            }

            var view = projection->Views[0];
            var entry = Described(view.SubImage.Swapchain, device);
            var rect = view.SubImage.ImageRect;
            if (entry is null || rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
                || (long)rect.X + rect.Width > entry.Description.Width
                || (long)rect.Y + rect.Height > entry.Description.Height
                || view.SubImage.ImageArrayIndex >= entry.Description.ArraySize)
            {
                return;
            }

            eye = new Eye(true, projection->Space, view.SubImage.Swapchain, rect, view.SubImage.ImageArrayIndex, view.Pose, view.Fov);
            return;
        }
    }

    /// <summary>The swapchain with its images and their description, listed on first sight. Null when they cannot be.</summary>
    private Swapchain? Described(ulong swapchain, ITextureDevice device)
    {
        var entry = Find(swapchain);
        if (entry is null)
        {
            entry = known[nextKnown];
            nextKnown = (nextKnown + 1) % MostSwapchains;
            entry.Forget(swapchain);
        }

        if (entry.Described)
        {
            return entry;
        }

        if (entry.Refused)
        {
            return null;
        }

        uint count = 0;
        if (calls.EnumerateSwapchainImages(swapchain, 0, &count, null) < XrResult.Success || count is 0 or > MostImages)
        {
            entry.Refused = true;
            return null;
        }

        var images = stackalloc XrSwapchainImageD3D11[(int)count];
        for (var i = 0; i < count; i++)
        {
            images[i] = new XrSwapchainImageD3D11 { Type = XrType.SwapchainImageD3D11 };
        }

        if (calls.EnumerateSwapchainImages(swapchain, count, &count, images) < XrResult.Success || images[0].Texture == 0)
        {
            entry.Refused = true;
            return null;
        }

        entry.Images = new nint[count];
        for (var i = 0; i < count; i++)
        {
            entry.Images[i] = images[i].Texture;
        }

        entry.Description = device.Describe(entry.Images[0]);
        entry.Described = true;
        return entry;
    }

    private Swapchain? Find(ulong swapchain)
    {
        foreach (var entry in known)
        {
            if (entry.Handle == swapchain && swapchain != 0)
            {
                return entry;
            }
        }

        return null;
    }

    private readonly record struct Eye(
        bool Valid,
        ulong Space,
        ulong Swapchain,
        XrRect2Di Rect,
        uint ArrayIndex,
        XrPosef Pose,
        XrFovf Fov);

    private sealed class Swapchain
    {
        public ulong Handle;
        public int LastAcquired = -1;
        public nint[] Images = [];
        public TextureDescription Description;
        public bool Described;
        public bool Refused;

        public void Forget(ulong handle)
        {
            Handle = handle;
            LastAcquired = -1;
            Images = [];
            Description = default;
            Described = false;
            Refused = false;
        }
    }
}
