using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using OverlaySpike.Gpu;
using OverlaySpike.Ui;
using OverlaySpike.Vr;
using Valve.VR;

namespace OverlaySpike;

public class SpikeApp : Application { }

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "help";
        try
        {
            return mode switch
            {
                "probe"  => RunProbe(),
                "vronly" => VrOnly(Arg(args, 1, 20)),
                "bench"  => Bench(Arg(args, 1, 400)),
                "snap"   => Snap(args.Length > 1 ? args[1] : "snap.png"),
                "run"    => Run(Arg(args, 1, 60), Arg(args, 2, 10)),
                _        => Help(),
            };
        }
        catch (Exception e) { Console.WriteLine("UNHANDLED: " + e); return 1; }
    }

    static int Arg(string[] a, int i, int def) => a.Length > i && int.TryParse(a[i], out var v) ? v : def;

    static int Help()
    {
        Console.WriteLine("modes:");
        Console.WriteLine("  probe                     what Avalonia 12 exposes on this machine");
        Console.WriteLine("  vronly  [seconds]         procedural pattern -> D3D11 -> IVROverlay (no Avalonia)");
        Console.WriteLine("  bench   [frames]          per-stage cost of the CPU path, no headset needed");
        Console.WriteLine("  snap    [file.png]        render the panel offscreen and save it, no headset needed");
        Console.WriteLine("  run     [seconds] [hz]    full end-to-end: Avalonia -> D3D11 -> IVROverlay");
        return 2;
    }

    static void StartAvalonia() => AppBuilder.Configure<SpikeApp>().UsePlatformDetect().SetupWithoutStarting();

    static int RunProbe() { StartAvalonia(); Ui.Probe.Run(); return 0; }

    // ---------------------------------------------------------------- vronly

    static int VrOnly(int seconds)
    {
        using var vr = new OverlayHost();
        var err = vr.Init("d47.spike.vronly", "d47 spike (procedural)");
        if (err != null) { Console.WriteLine("FAIL " + err); return 1; }
        Console.WriteLine($"overlay handle = {vr.Handle}");

        const int W = 1024, H = 640;
        using var d3d = D3D.Create(W, H);
        Console.WriteLine($"adapter        = {d3d.AdapterName} (DXGI index {d3d.AdapterIndex})");
        Console.WriteLine($"SteamVR wants  = DXGI adapter index {vr.PreferredAdapterIndex}"
                        + (vr.PreferredAdapterIndex == d3d.AdapterIndex ? "  [match]" : "  [MISMATCH - cross-adapter share]"));
        Console.WriteLine($"shared texture = 0x{d3d.Shared.NativePointer:X}  share handle = 0x{d3d.LegacyShareHandle:X}");

        var buf = Marshal.AllocHGlobal(W * H * 4);
        var sw = Stopwatch.StartNew();
        int frame = 0;
        var last = EVROverlayError.None;
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            Pattern.Fill(buf, W, H, frame);
            d3d.UploadBgra(buf, W * 4);
            d3d.Flush();
            last = vr.SubmitD3D11(d3d.Shared.NativePointer);
            if (last != EVROverlayError.None) { Console.WriteLine($"SetOverlayTexture -> {last}"); break; }
            vr.PumpEvents();
            if (frame % 60 == 0) Console.WriteLine($"frame {frame,5}  t={sw.Elapsed.TotalSeconds,5:F1}s  visible={vr.IsVisible}");
            frame++;
            System.Threading.Thread.Sleep(16);
        }
        Marshal.FreeHGlobal(buf);
        Console.WriteLine($"done. frames={frame} lastError={last} visible={vr.IsVisible}");
        return last == EVROverlayError.None ? 0 : 1;
    }

    // ----------------------------------------------------------------- bench

    static int Bench(int frames)
    {
        StartAvalonia();
        Console.WriteLine($"Avalonia {typeof(Application).Assembly.GetName().Version}  frames={frames} (first 50 discarded as warmup)");
        Console.WriteLine();
        foreach (var wh in new[] { (512, 320), (1024, 640), (1600, 900), (1920, 1080) })
            BenchSize(wh.Item1, wh.Item2, frames);
        return 0;
    }

    static void BenchSize(int w, int h, int frames)
    {
        const int Warmup = 50;
        var panel = new PanelView(w, h);
        using var rtb = new RenderTargetBitmap(new PixelSize(w, h));
        using var d3d = D3D.Create(w, h);
        if (w == 512) { Console.WriteLine($"adapter: {d3d.AdapterName}"); Console.WriteLine(); }
        var scratch = Marshal.AllocHGlobal(w * h * 4);

        var tTick = new Stage("layout+tick");
        var tRender = new Stage("rtb.Render");
        var tCopy = new Stage("CopyPixels->heap");
        var tUpA = new Stage("A: map/memcpy/copy");
        var tDirect = new Stage("B: CopyPixels->mapped");
        var tUpB = new Stage("B: unmap+copy");
        var tFlush = new Stage("Flush");

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < frames; i++)
        {
            tTick.Begin(); panel.Tick(sw.Elapsed.TotalSeconds, 0); tTick.End();
            tRender.Begin(); rtb.Render(panel); tRender.End();

            // Variant A: render -> heap buffer -> mapped staging -> shared texture
            tCopy.Begin(); rtb.CopyPixels(new PixelRect(0, 0, w, h), scratch, w * h * 4, w * 4); tCopy.End();
            tUpA.Begin(); d3d.UploadBgra(scratch, w * 4); tUpA.End();

            // Variant B: render -> straight into the mapped staging texture
            d3d.MapStaging(out var ptr, out var pitch);
            tDirect.Begin();
            rtb.CopyPixels(new MappedFramebuffer { Address = ptr, Size = new PixelSize(w, h), RowBytes = pitch });
            tDirect.End();
            tUpB.Begin(); d3d.UnmapAndCopy(); tUpB.End();

            tFlush.Begin(); d3d.Flush(); tFlush.End();
        }
        Marshal.FreeHGlobal(scratch);

        Console.WriteLine($"--- {w}x{h} ({w * h / 1000000.0:F2} Mpx, {w * h * 4 / 1048576.0:F2} MiB/frame) ---");
        foreach (var s in new[] { tTick, tRender, tCopy, tUpA, tDirect, tUpB, tFlush })
            Console.WriteLine("  " + s.Report(Warmup));
        var a = tTick.Mean(Warmup) + tRender.Mean(Warmup) + tCopy.Mean(Warmup) + tUpA.Mean(Warmup) + tFlush.Mean(Warmup);
        var b = tTick.Mean(Warmup) + tRender.Mean(Warmup) + tDirect.Mean(Warmup) + tUpB.Mean(Warmup) + tFlush.Mean(Warmup);
        Console.WriteLine($"  => variant A total {a:F3} ms/frame  ({1000 / a:F0} fps ceiling)");
        Console.WriteLine($"  => variant B total {b:F3} ms/frame  ({1000 / b:F0} fps ceiling)");
        Console.WriteLine($"  => at 10 Hz that is {a:F2}% of one core for A, {b:F2}% for B");
        Console.WriteLine();
    }

    /// Proof that the offscreen render is a real widget tree, not a blank surface.
    static int Snap(string path)
    {
        StartAvalonia();
        const int W = 1024, H = 640;
        var panel = new PanelView(W, H);
        using var rtb = new RenderTargetBitmap(new PixelSize(W, H));
        for (int i = 0; i < 42; i++) panel.Tick(i * 0.1, 60);
        rtb.Render(panel);
        rtb.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        Console.WriteLine($"wrote {path} ({W}x{H})");
        return 0;
    }

    // ------------------------------------------------------------------- run

    static int Run(int seconds, int hz)
    {
        StartAvalonia();
        const int W = 1024, H = 640;

        using var vr = new OverlayHost();
        var err = vr.Init("d47.spike.avalonia", "d47 spike (Avalonia)");
        if (err != null) { Console.WriteLine("FAIL " + err); return 1; }
        Console.WriteLine($"overlay handle = {vr.Handle}");

        var panel = new PanelView(W, H);
        using var rtb = new RenderTargetBitmap(new PixelSize(W, H));
        using var d3d = D3D.Create(W, H);
        Console.WriteLine($"adapter        = {d3d.AdapterName} (DXGI index {d3d.AdapterIndex})");
        Console.WriteLine($"SteamVR wants  = DXGI adapter index {vr.PreferredAdapterIndex}"
                        + (vr.PreferredAdapterIndex == d3d.AdapterIndex ? "  [match]" : "  [MISMATCH - cross-adapter share]"));
        Console.WriteLine($"shared texture = 0x{d3d.Shared.NativePointer:X}  share handle = 0x{d3d.LegacyShareHandle:X}");

        var tFrame = new Stage("whole frame");
        var tSubmit = new Stage("SetOverlayTexture");
        var sw = Stopwatch.StartNew();
        var period = TimeSpan.FromSeconds(1.0 / Math.Max(1, hz));
        int frame = 0; double fps = 0; var lastReport = TimeSpan.Zero;
        var last = EVROverlayError.None;

        while (sw.Elapsed.TotalSeconds < seconds)
        {
            var frameStart = sw.Elapsed;
            tFrame.Begin();
            panel.Tick(sw.Elapsed.TotalSeconds, fps);
            rtb.Render(panel);
            d3d.MapStaging(out var ptr, out var pitch);
            rtb.CopyPixels(new MappedFramebuffer { Address = ptr, Size = new PixelSize(W, H), RowBytes = pitch });
            d3d.UnmapAndCopy();
            d3d.Flush();
            tSubmit.Begin();
            last = vr.SubmitD3D11(d3d.Shared.NativePointer);
            tSubmit.End();
            tFrame.End();

            if (last != EVROverlayError.None) { Console.WriteLine($"SetOverlayTexture -> {last}"); break; }
            vr.PumpEvents();
            frame++;
            var elapsed = sw.Elapsed - frameStart;
            fps = elapsed.TotalSeconds > 0 ? 1.0 / elapsed.TotalSeconds : 0;
            if (sw.Elapsed - lastReport > TimeSpan.FromSeconds(2))
            {
                lastReport = sw.Elapsed;
                Console.WriteLine($"  t={sw.Elapsed.TotalSeconds,5:F1}s frame={frame,5} visible={vr.IsVisible}");
            }
            var sleep = period - elapsed;
            if (sleep > TimeSpan.Zero) System.Threading.Thread.Sleep(sleep);
        }

        Console.WriteLine();
        Console.WriteLine("  " + tFrame.Report(10));
        Console.WriteLine("  " + tSubmit.Report(10));
        Console.WriteLine($"done. frames={frame} lastError={last} visible={vr.IsVisible}");
        return last == EVROverlayError.None ? 0 : 1;
    }
}

/// Coloured rectangle with a sweeping bar and a binary frame counter, for the
/// no-Avalonia leg of the spike.
static class Pattern
{
    public static unsafe void Fill(IntPtr dst, int w, int h, int frame)
    {
        var p = (uint*)dst;
        const uint bg = 0xFF1A2B44, bar = 0xFFFFA640;
        int barX = (frame * 7) % w;
        for (int y = 0; y < h; y++)
        {
            uint rowBg = (y < 8 || y >= h - 8) ? 0xFF00FF80u : bg;
            for (int x = 0; x < w; x++)
                p[y * w + x] = (x >= barX && x < barX + 40) ? bar : rowBg;
        }
        for (int b = 0; b < 16; b++)
        {
            if ((frame & (1 << b)) == 0) continue;
            int x0 = 20 + b * 40;
            for (int y = 20; y < 60; y++)
                for (int x = x0; x < x0 + 30 && x < w; x++)
                    p[y * w + x] = 0xFFFFFFFF;
        }
    }
}
