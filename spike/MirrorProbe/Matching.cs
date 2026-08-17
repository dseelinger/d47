using OpenCvSharp;

namespace MirrorProbe;

/// <summary>
/// The measurement proper: take a panel out of one frame and try to find it in another.
/// <para>
/// This is deliberately the textbook method and nothing cleverer — detect, describe, match, ratio
/// test, RANSAC homography — because the question is whether the mirror carries enough for the
/// textbook method to work, not whether a sufficiently determined implementation could squeeze an
/// answer out of it. A result that only a bespoke pipeline can reach is a result the phase should
/// not be built on.
/// </para>
/// <para>
/// SURF is not an option and could not be reached by accident: it lives in nonfree contrib, and the
/// slim runtime package does not build contrib at all
/// (docs/spikes/screen-reading-licence-and-rules.md §4).
/// </para>
/// <para>
/// One wrinkle the licence spike could not have seen, because it read C++ headers and package
/// contents rather than the managed wrapper. ORB, AKAZE and BRISK are in the <c>OpenCvSharp</c>
/// namespace and <b>SIFT is in <c>OpenCvSharp.Features2D</c></b> — the namespace whose name says
/// contrib, <c>XFeatures2D</c>, is where SURF lives and SIFT does not. The native entry points say
/// the same thing and are the authority: <c>features2d_SIFT_create</c> beside
/// <c>xfeatures2d_SURF_create</c>. So the recommendation to reach for SIFT holds exactly as
/// written; only the using directive is a surprise.
/// </para>
/// </summary>
internal static class Matching
{
    /// <summary>Lowe's ratio. 0.75 is the usual figure and is not tuned to anything here.</summary>
    private const double LoweRatio = 0.75;

    /// <summary>What a homography is allowed to call an inlier, in pixels.</summary>
    private const double RansacThreshold = 3.0;

    /// <summary>
    /// What the panel region looks like before any matching — the answer to the item's actual
    /// question, which is whether there is anything in the mirror to read at all. A region with no
    /// texture cannot be matched by any method, so this closes the branch on its own if it is empty.
    /// </summary>
    internal sealed record Baseline(
        int Width,
        int Height,
        int Keypoints,
        double KeypointsPerMegapixel,
        double Sharpness,
        double MeanLuma,
        double Contrast);

    internal sealed record Match(
        string Target,
        int TargetWidth,
        int TargetHeight,
        int PanelKeypoints,
        int TargetKeypoints,
        int GoodMatches,
        int Inliers,
        double InlierRatio,
        double ReprojectionError,
        Point2f[]? Quad,
        bool QuadIsSane,
        string Verdict,
        string Note);

    internal static Feature2D Detector(string name) => name.ToLowerInvariant() switch
    {
        "orb" => ORB.Create(nFeatures: 5000),
        "sift" => OpenCvSharp.Features2D.SIFT.Create(nFeatures: 5000),
        "akaze" => AKAZE.Create(),
        "brisk" => BRISK.Create(),
        _ => throw new ArgumentException($"Unknown detector '{name}'. Use orb, sift, akaze or brisk."),
    };

    private static NormTypes NormFor(Feature2D detector) =>
        detector is OpenCvSharp.Features2D.SIFT ? NormTypes.L2 : NormTypes.Hamming;

    internal static Baseline Measure(Mat panel, Feature2D detector)
    {
        using var grey = ToGrey(panel);
        detector.DetectAndCompute(grey, null, out var keypoints, new Mat());

        using var laplacian = new Mat();
        Cv2.Laplacian(grey, laplacian, MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out var sharpness);
        Cv2.MeanStdDev(grey, out var luma, out var contrast);

        var megapixels = panel.Width * panel.Height / 1_000_000.0;

        return new Baseline(
            panel.Width,
            panel.Height,
            keypoints.Length,
            megapixels > 0 ? keypoints.Length / megapixels : 0,
            sharpness.Val0 * sharpness.Val0,
            luma.Val0,
            contrast.Val0);
    }

    internal static Match Find(Mat panel, Mat target, string targetName, Feature2D detector)
    {
        using var panelGrey = ToGrey(panel);
        using var targetGrey = ToGrey(target);

        using var panelDescriptors = new Mat();
        using var targetDescriptors = new Mat();

        detector.DetectAndCompute(panelGrey, null, out var panelKeys, panelDescriptors);
        detector.DetectAndCompute(targetGrey, null, out var targetKeys, targetDescriptors);

        Match Failed(string note, int good = 0) => new(
            targetName,
            target.Width,
            target.Height,
            panelKeys.Length,
            targetKeys.Length,
            good,
            0,
            0,
            0,
            null,
            false,
            "not located",
            note);

        if (panelDescriptors.Empty() || targetDescriptors.Empty())
        {
            return Failed("One side produced no descriptors at all.");
        }

        using var matcher = new BFMatcher(NormFor(detector), crossCheck: false);
        var candidates = matcher.KnnMatch(panelDescriptors, targetDescriptors, 2);

        var good = candidates
            .Where(pair => pair.Length == 2 && pair[0].Distance < LoweRatio * pair[1].Distance)
            .Select(pair => pair[0])
            .ToArray();

        if (good.Length < 4)
        {
            return Failed("Fewer than four matches survived the ratio test, so no homography is possible.", good.Length);
        }

        var source = good.Select(m => Wide(panelKeys[m.QueryIdx].Pt)).ToArray();
        var destination = good.Select(m => Wide(targetKeys[m.TrainIdx].Pt)).ToArray();

        using var mask = new Mat();
        using var homography = Cv2.FindHomography(source, destination, HomographyMethods.Ransac, RansacThreshold, mask);

        if (homography.Empty())
        {
            return Failed("RANSAC could not agree on a homography.", good.Length);
        }

        mask.GetArray(out byte[] inlierFlags);
        var inliers = inlierFlags.Count(flag => flag != 0);

        var corners = new[]
        {
            new Point2f(0, 0),
            new Point2f(panel.Width, 0),
            new Point2f(panel.Width, panel.Height),
            new Point2f(0, panel.Height),
        };

        var quad = Cv2.PerspectiveTransform(corners, homography);

        var projected = Cv2.PerspectiveTransform(source.Select(p => new Point2f((float)p.X, (float)p.Y)), homography);

        var error = 0.0;

        for (var i = 0; i < projected.Length; i++)
        {
            if (inlierFlags[i] == 0)
            {
                continue;
            }

            var dx = projected[i].X - destination[i].X;
            var dy = projected[i].Y - destination[i].Y;
            error += Math.Sqrt((dx * dx) + (dy * dy));
        }

        error = inliers > 0 ? error / inliers : 0;

        var sane = IsSane(quad, target.Width, target.Height, out var sanity);
        var ratio = good.Length > 0 ? (double)inliers / good.Length : 0;

        // Deliberately blunt. A located panel under RANSAC is a dozen or more agreeing points and a
        // quadrilateral that is still a quadrilateral; anything less is reported as not located
        // rather than as a weak yes, because a weak yes is the confident wrong answer this phase is
        // most afraid of.
        var located = inliers >= 12 && ratio >= 0.25 && sane;

        return new Match(
            targetName,
            target.Width,
            target.Height,
            panelKeys.Length,
            targetKeys.Length,
            good.Length,
            inliers,
            ratio,
            error,
            quad,
            sane,
            located ? "located" : "not located",
            sane ? string.Empty : sanity);
    }

    /// <summary>
    /// A homography will happily fold the panel into a bow tie or a sliver and still call it a fit.
    /// Rejecting those is the difference between a located panel and a number that looks like one.
    /// </summary>
    private static bool IsSane(Point2f[] quad, int width, int height, out string reason)
    {
        reason = string.Empty;

        var points = quad.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();

        if (!Cv2.IsContourConvex(points))
        {
            reason = "The projected quadrilateral is not convex — the homography folded it over.";
            return false;
        }

        var area = Math.Abs(Cv2.ContourArea(points));
        var frame = (double)width * height;

        if (area < frame * 0.001)
        {
            reason = $"The projected quadrilateral covers {area / frame:P2} of the frame, which is a collapse.";
            return false;
        }

        if (area > frame * 1.5)
        {
            reason = $"The projected quadrilateral covers {area / frame:P0} of the frame, which is a blow-up.";
            return false;
        }

        return true;
    }

    internal static Mat Annotate(Mat target, Point2f[]? quad, string caption)
    {
        var annotated = target.Clone();

        if (quad is not null)
        {
            var points = quad.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
            Cv2.Polylines(annotated, [points], isClosed: true, Scalar.LimeGreen, thickness: 3);
        }

        Cv2.PutText(annotated, caption, new Point(16, 40), HersheyFonts.HersheySimplex, 1.0, Scalar.LimeGreen, 2);
        return annotated;
    }

    private static Point2d Wide(Point2f point) => new(point.X, point.Y);

    private static Mat ToGrey(Mat image)
    {
        if (image.Channels() == 1)
        {
            return image.Clone();
        }

        var grey = new Mat();
        Cv2.CvtColor(image, grey, ColorConversionCodes.BGR2GRAY);
        return grey;
    }
}
