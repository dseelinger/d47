using System.Numerics;
using System.Runtime.InteropServices;
using D47.Core;
using D47.Core.Vr;
using D47.Vr;
using Microsoft.Extensions.Logging;

namespace GrabSpike;

/// <summary>
/// Three questions the shipping build cannot answer from outside a headset, asked one at a time.
/// <para>
/// This drives the <em>real</em> <see cref="SteamVrRuntime"/>, <see cref="VrActionInput"/> and
/// <see cref="VrRay"/> — not a copy — with the Avalonia rasteriser left out, so a panel is a
/// generated checkerboard rather than the widget tree. Everything it proves therefore applies to
/// what ships.
/// </para>
/// <list type="number">
/// <item><description><b>Do controller poses move?</b> The reported symptom is a ray frozen where
/// it was when it appeared, which is what a pose read once and cached looks like. The telemetry
/// line prints each hand's position and flags whether it changed since the last frame, so "frozen"
/// and "never found" stop looking alike.</description></item>
/// <item><description><b>Does the trigger arrive?</b> Registration succeeding says the manifest
/// loaded, not that SteamVR bound anything to it. This prints the digital action's
/// <c>bActive</c> and <c>bState</c> separately, which is the difference between "bound to nothing"
/// and "bound, not pressed".</description></item>
/// <item><description><b>Does an absolutely-placed quad stay put?</b> The panel here is always
/// world-locked, placed once, and never re-placed unless carried — so if it follows the head,
/// the placement path is wrong rather than the setting.</description></item>
/// </list>
/// </summary>
internal static class Program
{
    private const int PanelPx = 512;
    private const int PanelRows = 320;

    public static int Main(string[] args)
    {
        Console.WriteLine("d47 grab spike — Ctrl+C to stop.");
        Console.WriteLine();

        using var loggers = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Debug)
            .AddSimpleConsole(o => o.SingleLine = true));

        // Its own data folder, so a run cannot disturb the installed build's settings or its
        // action manifest while that copy may be open.
        var paths = new AppPaths(Path.Combine(Path.GetTempPath(), "d47-grab-spike"));
        Directory.CreateDirectory(paths.Data);

        var runtime = new SteamVrRuntime([], loggers.CreateLogger<SteamVrRuntime>());

        var start = runtime.Start();

        // Explicit, because registering the d47 application key with SteamVR is now something a
        // process opts into rather than a side effect of starting a session. The spike opts in;
        // the test suite must not, which is the whole reason it moved.
        if (start.Outcome == VrStartOutcome.Started)
        {
            runtime.Actions.Register(paths.VrActions);
        }

        Console.WriteLine($"start: {start.Outcome} — {start.Detail}");

        if (start.Outcome != VrStartOutcome.Started)
        {
            Console.WriteLine("SteamVR is not up. Start it, put the headset on, and run this again.");
            return 1;
        }

        try
        {
            Run(runtime, args.Contains("--head", StringComparer.Ordinal));
        }
        finally
        {
            runtime.Stop();
        }

        return 0;
    }

    private static void Run(SteamVrRuntime runtime, bool headLocked)
    {
        var panel = VrOverlay.Create("com.dseelinger.D47.spike", "d47 spike panel", out var failure);

        if (panel is null)
        {
            Console.WriteLine($"no panel quad: {failure.Detail}");
            return;
        }

        using (panel)
        {
            var pixels = Checkerboard();
            var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);

            try
            {
                panel.Submit(pinned.AddrOfPinnedObject(), PanelPx, PanelRows);
            }
            finally
            {
                pinned.Free();
            }

            var placement = new SurfacePlacement
            {
                Lock = SurfaceLock.WorldLocked,
                DistanceMetres = 1.1f,
                DropMetres = -0.25f,
                WidthMetres = 0.6f,
                Curvature = 0f,
                Opacity = 1f,
            };

            var extent = new VrExtent(placement.WidthMetres, (float)PanelPx / PanelRows);

            panel.Look(placement.WidthMetres, placement.Curvature, placement.Opacity);
            panel.Show(true);

            Loop(runtime, panel, placement, extent, headLocked);
        }
    }

    private static void Loop(
        SteamVrRuntime runtime,
        VrOverlay panel,
        SurfacePlacement placement,
        VrExtent extent,
        bool headLocked)
    {
        VrPose? resting = null;
        Matrix4x4? carrying = null;
        var carryingHand = 0u;

        var lastSeen = new Dictionary<uint, Vector3>();
        var first = new Dictionary<uint, Vector3>();
        var range = new Dictionary<uint, float>();
        var printed = DateTimeOffset.MinValue;
        var frames = 0;
        var movedFrames = 0;

        if (runtime.Beam is null || runtime.Cursor is null)
        {
            Console.WriteLine();
            Console.WriteLine("!! No beam and/or cursor quad. Almost certainly KeyInUse: the installed d47");
            Console.WriteLine("!! already owns those overlay keys. Close d47, or switch its VR off, and rerun.");
        }

        Console.WriteLine();
        Console.WriteLine(">> PICK THE CONTROLLERS UP AND WAVE THEM. <<");
        Console.WriteLine("A controller resting on a desk reports a constant pose quite legitimately, so");
        Console.WriteLine("moved=0 only means something if you were moving while it was counting.");
        Console.WriteLine();
        Console.WriteLine("head=HMD | hands=device:(x,y,z) | moved=frames-that-changed/total | trig=state | hit=u,v@m");
        Console.WriteLine();

        while (true)
        {
            var now = DateTimeOffset.UtcNow;

            if (!runtime.Serve(now))
            {
                Console.WriteLine("the session went away; stopping");
                return;
            }

            if (runtime.Head is not { } head)
            {
                Thread.Sleep(11);
                continue;
            }

            // Placed once and then left alone. Anything that moves it after this is the placement
            // path misbehaving rather than a setting.
            resting ??= VrPlacementMath.Resting(head, placement.DistanceMetres, head.Position.Y - 0.35f, extent.HeightMetres);

            var hands = runtime.Controllers();
            var surface = carrying is { } grabbed && Hand(hands, carryingHand) is { } carrier
                ? VrPlacementMath.Carried(grabbed, carrier.Aim)
                : resting.Value;

            var found = VrRay.PointingAt(hands, surface, extent, placement.Curvature);
            var held = runtime.Actions.TriggerHeld(found is not null || carrying is not null);

            // Grab, carry, release — the same three rules the app uses.
            if (!held)
            {
                if (carrying is not null)
                {
                    resting = surface;
                    carrying = null;
                    Console.WriteLine($"  [{now:HH:mm:ss}] put down");
                }
            }
            else if (carrying is null && found is { } on)
            {
                carrying = VrPlacementMath.Grab(on.Hand.Aim, surface);
                carryingHand = on.Hand.Device;
                Console.WriteLine($"  [{now:HH:mm:ss}] picked up by device {on.Hand.Device}");
            }

            if (headLocked)
            {
                panel.PlaceOnHead(placement.AgainstTheHead());
            }
            else
            {
                panel.PlaceAbsolute(surface);
            }

            runtime.AimBeam(
                found?.Hand.Aim ?? (carrying is not null ? Hand(hands, carryingHand)?.Aim : null),
                head,
                found?.Hit.DistanceMetres ?? VrAim.BeamMissLengthMetres);

            runtime.ShowCursor(
                found is { } at ? VrAim.PointAlong(at.Hand.Aim, at.Hit.DistanceMetres) : null,
                head);

            // Did anything actually move this frame? This is the whole first question.
            frames++;

            foreach (var hand in hands)
            {
                if (lastSeen.TryGetValue(hand.Device, out var was))
                {
                    if (Vector3.Distance(was, hand.Aim.Position) > 0.001f)
                    {
                        movedFrames++;
                    }

                    // How far it has strayed from the first place it was ever seen. A counter can
                    // be fooled by jitter; a range of a millimetre over a minute of waving cannot.
                    if (first.TryGetValue(hand.Device, out var origin))
                    {
                        var strayed = Vector3.Distance(origin, hand.Aim.Position);
                        range[hand.Device] = MathF.Max(range.GetValueOrDefault(hand.Device), strayed);
                    }
                }
                else
                {
                    first[hand.Device] = hand.Aim.Position;
                }

                lastSeen[hand.Device] = hand.Aim.Position;
            }

            if (now - printed > TimeSpan.FromMilliseconds(400))
            {
                printed = now;
                Report(head, hands, range, held, runtime, found, frames, movedFrames);
            }

            // Roughly headset rate. The app's tick is 10 Hz, which is worth remembering if the
            // beam here is smooth and the beam there is not.
            Thread.Sleep(11);
        }
    }

    private static void Report(
        VrPose head,
        IReadOnlyList<VrHand> hands,
        Dictionary<uint, float> range,
        bool held,
        SteamVrRuntime runtime,
        (VrHand Hand, VrHit Hit)? found,
        int frames,
        int movedFrames)
    {
        var where = string.Join("  ", hands.Select(hand =>
            $"{hand.Device}:({hand.Aim.Position.X:0.00},{hand.Aim.Position.Y:0.00},{hand.Aim.Position.Z:0.00})"
            + $"~{range.GetValueOrDefault(hand.Device):0.00}m"));

        if (hands.Count == 0)
        {
            where = "NONE FOUND";
        }

        var hit = found is { } on
            ? $"{on.Hit.U:0.00},{on.Hit.V:0.00}@{on.Hit.DistanceMetres:0.00}m"
            : "-";

        Console.WriteLine(
            $"head=({head.Position.X:0.00},{head.Position.Y:0.00},{head.Position.Z:0.00})  "
            + $"hands={where}  moved={movedFrames}/{frames}  "
            + $"trig={(runtime.Actions.Ready ? "ready" : "NOT-REGISTERED")}/{(held ? "DOWN" : "up")}"
            + $"{(runtime.Actions.HoldingPriority ? "/claimed" : string.Empty)}  hit={hit}");
    }

    private static VrHand? Hand(IReadOnlyList<VrHand> hands, uint device)
    {
        foreach (var hand in hands)
        {
            if (hand.Device == device)
            {
                return hand;
            }
        }

        return null;
    }

    /// <summary>
    /// A panel whose orientation is unmistakable: coarse checks, a bright top-left corner and a
    /// dimmer bottom edge. A uniform quad cannot tell you it is upside down or mirrored, which is
    /// exactly the class of fault this is looking for.
    /// </summary>
    private static byte[] Checkerboard()
    {
        var pixels = new byte[PanelPx * PanelRows * 4];

        for (var y = 0; y < PanelRows; y++)
        {
            for (var x = 0; x < PanelPx; x++)
            {
                var check = ((x / 32) + (y / 32)) % 2 == 0;
                var topLeft = x < PanelPx / 6 && y < PanelRows / 6;
                var bottom = y > PanelRows - 24;

                byte r = topLeft ? (byte)255 : check ? (byte)40 : (byte)15;
                byte g = topLeft ? (byte)176 : check ? (byte)40 : (byte)15;
                byte b = topLeft ? (byte)64 : check ? (byte)48 : (byte)18;

                if (bottom)
                {
                    r = 80;
                    g = 20;
                    b = 20;
                }

                var at = ((y * PanelPx) + x) * 4;
                pixels[at] = r;
                pixels[at + 1] = g;
                pixels[at + 2] = b;
                pixels[at + 3] = 255;
            }
        }

        return pixels;
    }
}
