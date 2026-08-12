using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

namespace OverlaySpike.Ui;

sealed class LeaseProbe : ICustomDrawOperation
{
    public Rect Bounds { get; set; }
    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation other) => false;
    public void Dispose() { }
    public void Render(ImmediateDrawingContext context)
    {
        var feat = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        Console.WriteLine($"  lease feature : {feat?.GetType().FullName ?? "<null>"}");
        if (feat == null) return;
        using var lease = feat.Lease();
        Console.WriteLine($"  GrContext     : {(lease.GrContext == null ? "<null>  => CPU raster surface" : lease.GrContext.GetType().Name + " (GPU)")}");
        Console.WriteLine($"  SkSurface     : {lease.SkSurface?.GetType().Name}");
        var pl = lease.TryLeasePlatformGraphicsApi();
        Console.WriteLine($"  PlatformGfx   : {pl?.Context?.GetType().FullName ?? "<null>"}");
        pl?.Dispose();
    }
}

static class Probe
{
    public static void Run()
    {
        Console.WriteLine($"=== Avalonia {typeof(Application).Assembly.GetName().Version} on {Environment.OSVersion} ===\n");

        Console.WriteLine("-- interfaces Avalonia 12 blocks external implementation of --");
        foreach (var tn in new[] {
            "Avalonia.Platform.ITopLevelImpl, Avalonia.Controls",
            "Avalonia.Platform.Surfaces.IPlatformRenderSurface, Avalonia.Base",
            "Avalonia.Platform.Surfaces.IFramebufferRenderTarget, Avalonia.Base",
            "Avalonia.Platform.IRenderTarget, Avalonia.Base",
            "Avalonia.OpenGL.Surfaces.IGlPlatformSurface, Avalonia.OpenGL",
        })
        {
            var t = Type.GetType(tn);
            if (t == null) { Console.WriteLine($"  {tn} -> NOT FOUND"); continue; }
            var all = new System.Collections.Generic.List<Type> { t };
            all.AddRange(t.GetInterfaces());
            var blocked = false;
            foreach (var i in all)
            {
                foreach (var m in i.GetMembers(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (m.Name.Contains("implementable")) { Console.WriteLine($"  {t.Name,-26} BLOCKED by {i.Name}.{m.Name}"); blocked = true; }
                    else if (m is MethodInfo mm && !mm.IsPublic && !mm.IsSpecialName) { Console.WriteLine($"  {t.Name,-26} non-public member {i.Name}.{m.Name}"); blocked = true; }
                }
            }
            if (!blocked) Console.WriteLine($"  {t.Name,-26} no marker found (members: {string.Join(",", t.GetMembers().Take(3).Select(x=>x.Name))})");
        }

        Console.WriteLine("\n-- default compositor / platform graphics --");
        var comp = Compositor.TryGetDefaultCompositor();
        Console.WriteLine($"  Compositor.TryGetDefaultCompositor() -> {comp?.GetType().FullName ?? "<null>"}");
        if (comp != null)
        {
            // NB: this must not be blocked on without a running dispatcher - it deadlocks.
            var task = System.Threading.Tasks.Task.Run(async () => await comp.TryGetCompositionGpuInterop());
            var interop = task.Wait(TimeSpan.FromSeconds(5)) ? task.Result : null;
            if (!task.IsCompleted) Console.WriteLine("  (GpuInterop query timed out - needs a running dispatcher)");
            Console.WriteLine($"  GpuInterop -> {interop?.GetType().FullName ?? "<null>"}");
            if (interop != null)
            {
                Console.WriteLine($"    SupportedImageHandleTypes: {string.Join(", ", interop.SupportedImageHandleTypes)}");
                Console.WriteLine($"    SupportedSemaphoreTypes  : {string.Join(", ", interop.SupportedSemaphoreTypes)}");
            }
        }

        Console.WriteLine("\n-- RenderTargetBitmap backing --");
        using var rtb = new RenderTargetBitmap(new PixelSize(256, 128));
        using (var dc = rtb.CreateDrawingContext())
        {
            dc.FillRectangle(Brushes.Red, new Rect(0, 0, 256, 128));
            dc.Custom(new LeaseProbe { Bounds = new Rect(0, 0, 256, 128) });
        }
        Console.WriteLine($"  RenderTargetBitmap is a Bitmap: {rtb is Bitmap}  Format={rtb.Format}");
    }
}
