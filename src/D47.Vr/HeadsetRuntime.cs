using System.Numerics;
using D47.Core.Vr;
using Microsoft.Extensions.Logging;

namespace D47.Vr;

/// <summary>
/// Chooses, at each start, between the SteamVR overlay and d47's OpenXR layer, and passes every call to the
/// one that started. The layer is chosen only while <paramref name="layerWanted"/> says so and the layer is in
/// an Elite session on a runtime that is not SteamVR's.
/// </summary>
public sealed class HeadsetRuntime(
    IVrSession steamVr,
    OpenXrLayerRuntime layer,
    Func<bool> layerWanted,
    ILogger<HeadsetRuntime> logger) : IVrSession
{
    /// <summary>Added to SteamVR's reason when the layer route is allowed and absent too.</summary>
    public const string NoLayerEither = "No OpenXR runtime has the D47 layer in an Elite session either.";

    private volatile IVrSession? _live;
    private bool _pointing;

    /// <summary>The SteamVR runtime while its route is the live one, otherwise null.</summary>
    public IVrSession? SteamVr => _live == steamVr ? steamVr : null;

    public bool Pointing
    {
        get => _pointing;
        set
        {
            _pointing = value;
            steamVr.Pointing = value;
            layer.Pointing = value;
        }
    }

    public bool SupportsCurvature => _live?.SupportsCurvature ?? true;

    public VrPose? Head => _live?.Head;

    public VrStart Start()
    {
        _live = null;

        if (!layerWanted())
        {
            return Started(steamVr, steamVr.Start());
        }

        var seen = layer.Look();

        if (seen.Present && !seen.OnSteamVr)
        {
            return Started(layer, layer.Start(seen));
        }

        var start = Started(steamVr, steamVr.Start());

        if (start.Outcome == VrStartOutcome.Started)
        {
            return start;
        }

        if (seen.Mapped && seen.Magic == D47.XrLayer.XrBlock.MagicValue
            && seen.LayoutVersion != D47.XrLayer.XrBlock.CurrentLayoutVersion)
        {
            return layer.Start(seen);
        }

        if (seen.Present)
        {
            return start;
        }

        var outcome = start.Outcome == VrStartOutcome.NoRuntime ? VrStartOutcome.NotReady : start.Outcome;
        var said = start.Detail is { Length: > 0 } detail ? $"{detail} {NoLayerEither}" : NoLayerEither;
        return new VrStart(outcome, said);
    }

    public bool Serve(DateTimeOffset now)
    {
        if (_live is not { } live)
        {
            return false;
        }

        if (live == layer && !layerWanted())
        {
            logger.LogInformation("The OpenXR layer route was switched off; leaving it");
            return false;
        }

        if (live == steamVr && layerWanted() && layer.Look() is { Present: true, OnSteamVr: false, Moving: true } seen)
        {
            logger.LogInformation(
                "The D47 layer is in an Elite session on {Runtime}; leaving SteamVR's overlay for it",
                seen.RuntimeName);
            return false;
        }

        return live.Serve(now);
    }

    public void Stop()
    {
        var live = _live;
        _live = null;
        live?.Stop();
    }

    public (IReadOnlyList<VrHand> Hands, VrPose? Head) HandsAndHead() =>
        _live is { } live ? live.HandsAndHead() : ([], null);

    public void PrepareInput() => _live?.PrepareInput();

    public bool TriggerHeld(bool wanted) => _live?.TriggerHeld(wanted) ?? false;

    public bool BackPressed() => _live?.BackPressed() ?? false;

    public void ReleaseInput() => _live?.ReleaseInput();

    public void Reposition(VrSurface surface, VrPose where) => _live?.Reposition(surface, where);

    public void AimBeam(VrPose? along, VrPose head, float lengthMetres) => _live?.AimBeam(along, head, lengthMetres);

    public void ShowCursor(Vector3? at, VrPose head, VrCursor kind) => _live?.ShowCursor(at, head, kind);

    private VrStart Started(IVrSession route, VrStart start)
    {
        if (start.Outcome == VrStartOutcome.Started)
        {
            _live = route;
        }

        return start;
    }
}
