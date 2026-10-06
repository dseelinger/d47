#if DEBUG
using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.OpenGL;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using D47.Core.Hulls;

namespace D47.App.Panel;

/// <summary>
/// <c>d47.exe --capture-hull-gpu &lt;mesh&gt; &lt;out&gt;</c>: draws one frame of the mesh through <see cref="HullGlView"/> at
/// <see cref="HullCamera.Rest"/> and writes it to <c>out</c>. Debug builds only.
/// </summary>
/// <remarks>
/// The file is two little-endian int32s, width then height, then width × height BGRA pixels, top row first.
/// </remarks>
internal static class HullGpuCapture
{
    internal const string Flag = "--capture-hull-gpu";
    internal const int Width = 640;
    internal const int Height = 360;

    /// <summary>The shade the committed frame was drawn with.</summary>
    internal static readonly HullShade Shade = new(0xFFC8C8C8, 0xFF101418);

    private const int Rgba = 0x1908;
    private const int UnsignedByte = 0x1401;
    private const int Timeout = 20;

    private const int PassedExitCode = 0;
    private const int UsageExitCode = 1;
    private const int MeshExitCode = 2;
    private const int FailedExitCode = 3;
    private const int TimedOutExitCode = 4;

    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, Flag);

        if (at < 0 || at + 2 >= args.Length)
        {
            Console.Error.WriteLine($"usage: d47.exe {Flag} <mesh> <out>");
            return UsageExitCode;
        }

        HullMesh? mesh;

        using (var stream = File.OpenRead(args[at + 1]))
        {
            mesh = HullMesh.Read(stream);
        }

        if (mesh is null)
        {
            Console.Error.WriteLine($"{args[at + 1]} is not a hull mesh");
            return MeshExitCode;
        }

        var capture = new Capture(mesh, args[at + 2]);

        AppBuilder.Configure(() => capture)
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime([]);

        return capture.ExitCode;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void ReadPixelsProc(int x, int y, int width, int height, int format, int type, IntPtr data);

    private sealed class Capture(HullMesh mesh, string output) : Application
    {
        public int ExitCode { get; private set; } = TimedOutExitCode;

        public override void Initialize() => Styles.Add(new FluentTheme());

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            var pose = new HullPose(mesh);
            var view = new HullGlView(pose, () => Shade);
            var window = new Window { Width = Width + 100, Height = Height + 100, CanResize = false, Content = view };

            view.Failed += reason =>
            {
                Console.Error.WriteLine($"the GPU draw failed: {reason}");
                Finish(desktop, FailedExitCode);
            };

            view.Drawn += (gl, width, height) =>
            {
                if (width != Width || height != Height)
                {
                    return;
                }

                Write(gl, width, height);
                Finish(desktop, PassedExitCode);
            };

            window.Opened += (_, _) =>
            {
                var scale = window.RenderScaling;
                view.Width = Width / scale;
                view.Height = Height / scale;
            };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Timeout) };

            timer.Tick += (_, _) =>
            {
                Console.Error.WriteLine($"no {Width}×{Height} frame within {Timeout} seconds");
                Finish(desktop, TimedOutExitCode);
            };

            timer.Start();
            desktop.MainWindow = window;
        }

        private void Finish(IClassicDesktopStyleApplicationLifetime desktop, int code)
        {
            if (ExitCode == TimedOutExitCode || code != TimedOutExitCode)
            {
                ExitCode = code;
            }

            // Not from inside a render: closing the window there tears the context down under the draw.
            Dispatcher.UIThread.Post(() => desktop.Shutdown());
        }

        private void Write(GlInterface gl, int width, int height)
        {
            var rgba = new byte[width * height * 4];
            var pinned = GCHandle.Alloc(rgba, GCHandleType.Pinned);

            try
            {
                var address = gl.GetProcAddress("glReadPixels");
                var readPixels = Marshal.GetDelegateForFunctionPointer<ReadPixelsProc>(address);
                readPixels(0, 0, width, height, Rgba, UnsignedByte, pinned.AddrOfPinnedObject());
            }
            finally
            {
                pinned.Free();
            }

            // OpenGL reads bottom row first, in RGBA.
            var bgra = new byte[rgba.Length];

            for (var y = 0; y < height; y++)
            {
                var from = (height - 1 - y) * width * 4;
                var to = y * width * 4;

                for (var x = 0; x < width * 4; x += 4)
                {
                    bgra[to + x] = rgba[from + x + 2];
                    bgra[to + x + 1] = rgba[from + x + 1];
                    bgra[to + x + 2] = rgba[from + x];
                    bgra[to + x + 3] = rgba[from + x + 3];
                }
            }

            using var file = File.Create(output);
            using var writer = new BinaryWriter(file);
            writer.Write(width);
            writer.Write(height);
            writer.Write(bgra);
        }
    }
}
#endif
