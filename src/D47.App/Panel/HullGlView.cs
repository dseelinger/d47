using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using D47.Core.Hulls;

namespace D47.App.Panel;

/// <summary>
/// A hull mesh drawn through OpenGL into a 4× multisampled framebuffer, lit as <see cref="HullRasteriser"/>
/// lights it. Draws only when asked. Raises <see cref="Failed"/>, possibly mid-render, when it cannot draw.
/// </summary>
internal sealed class HullGlView : OpenGlControlBase
{
    private const int Samples = 4;

    private const int ArrayBuffer = 0x8892;
    private const int ElementArrayBuffer = 0x8893;
    private const int StaticDraw = 0x88E4;
    private const int Float = 0x1406;
    private const int UnsignedInt = 0x1405;
    private const int Triangles = 0x0004;
    private const int VertexShader = 0x8B31;
    private const int FragmentShader = 0x8B30;
    private const int Framebuffer = 0x8D40;
    private const int ReadFramebuffer = 0x8CA8;
    private const int DrawFramebuffer = 0x8CA9;
    private const int Renderbuffer = 0x8D41;
    private const int Rgba8 = 0x8058;
    private const int DepthComponent24 = 0x81A6;
    private const int ColorAttachment0 = 0x8CE0;
    private const int DepthAttachment = 0x8D00;
    private const int FramebufferComplete = 0x8CD5;
    private const int ColorBufferBit = 0x4000;
    private const int DepthBufferBit = 0x0100;
    private const int DepthTest = 0x0B71;
    private const int CullFace = 0x0B44;
    private const int Less = 0x0201;
    private const int Nearest = 0x2600;

    private const string VertexSource = """
        in vec3 position;
        in vec3 normal;
        uniform mat4 mvp;
        uniform mat4 view;
        uniform vec3 toLight;
        uniform float light;
        uniform float ambient;
        out float lit;

        void main()
        {
            vec4 clip = mvp * vec4(position, 1.0);
            vec3 n = (view * vec4(normal, 0.0)).xyz;
            n = dot(n, n) > 1e-12 ? normalize(n) : vec3(0.0);
            if (dot(n, -(view * vec4(position, 1.0)).xyz) < 0.0)
            {
                n = -n;
            }

            lit = clamp((ambient + ((1.0 - ambient) * max(0.0, dot(n, toLight)))) * light, 0.0, 1.0);

            // The projection's depth runs 0 to w; OpenGL clips at -w to w.
            gl_Position = vec4(clip.xy, (2.0 * clip.z) - clip.w, clip.w);
        }
        """;

    private const string FragmentSource = """
        in float lit;
        uniform vec4 hull;
        uniform vec4 background;
        out vec4 colour;

        void main()
        {
            colour = mix(background, hull, lit);
        }
        """;

    private readonly HullPose _pose;
    private readonly Func<HullShade> _shade;

    private RenderbufferStorageMultisampleProc? _storageMultisample;
    private Uniform3fProc? _uniform3f;
    private Uniform4fProc? _uniform4f;
    private UniformMatrix4fvProc? _uniformMatrix4fv;

    private int _program;
    private int _vertexArray;
    private int _vertices;
    private int _indices;
    private int _indexCount;

    private int _framebuffer;
    private int _colour;
    private int _depth;
    private int _width;
    private int _height;

    private int _mvpAt;
    private int _viewAt;
    private int _toLightAt;
    private int _lightAt;
    private int _ambientAt;
    private int _hullAt;
    private int _backgroundAt;

    private bool _failed;

    internal HullGlView(HullPose pose, Func<HullShade> shade)
    {
        _pose = pose;
        _shade = shade;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void RenderbufferStorageMultisampleProc(int target, int samples, int format, int width, int height);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void Uniform3fProc(int location, float x, float y, float z);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void Uniform4fProc(int location, float x, float y, float z, float w);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void UniformMatrix4fvProc(int location, int count, [MarshalAs(UnmanagedType.U1)] bool transpose, float[] value);

    /// <summary>Why this view cannot draw; the viewer swaps to the CPU on it.</summary>
    internal event Action<string>? Failed;

    /// <summary>Raised after each frame is blitted to the control's framebuffer, which is still bound; the arguments are the GL interface, the width and the height in pixels.</summary>
    internal event Action<GlInterface, int, int>? Drawn;

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        try
        {
            Init(gl);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or MarshalDirectiveException or EntryPointNotFoundException)
        {
            Fail($"OpenGL could not be set up: {ex.Message}");
        }
    }

    protected override void OnOpenGlLost()
    {
        base.OnOpenGlLost();

        Fail("the OpenGL context was lost");
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        DeleteTargets(gl);

        if (_program != 0)
        {
            gl.DeleteProgram(_program);
        }

        if (_vertexArray != 0)
        {
            gl.DeleteVertexArray(_vertexArray);
        }

        if (_vertices != 0)
        {
            gl.DeleteBuffer(_vertices);
        }

        if (_indices != 0)
        {
            gl.DeleteBuffer(_indices);
        }

        _program = _vertexArray = _vertices = _indices = 0;

        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_failed || _program == 0 || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var width = Math.Max(1, (int)(Bounds.Width * top.RenderScaling));
        var height = Math.Max(1, (int)(Bounds.Height * top.RenderScaling));

        try
        {
            Fit(gl, width, height);
        }
        catch (InvalidOperationException ex)
        {
            Fail(ex.Message);
            return;
        }

        var shade = _shade();
        var background = Channels(shade.Background);
        var hull = Channels(shade.Hull);
        var camera = _pose.Camera;
        var view = camera.View();

        gl.BindFramebuffer(Framebuffer, _framebuffer);
        gl.Viewport(0, 0, width, height);
        gl.ClearColor(background.X, background.Y, background.Z, background.W);
        gl.ClearDepth(1f);
        gl.Enable(DepthTest);
        gl.DepthFunc(Less);
        gl.DepthMask(1);
        gl.Disable(CullFace);
        gl.Clear(ColorBufferBit | DepthBufferBit);

        gl.UseProgram(_program);
        _uniformMatrix4fv!(_mvpAt, 1, false, Floats(view * camera.Projection((float)width / height)));
        _uniformMatrix4fv(_viewAt, 1, false, Floats(view));
        _uniform3f!(_toLightAt, HullRasteriser.LightDirection.X, HullRasteriser.LightDirection.Y, HullRasteriser.LightDirection.Z);
        gl.Uniform1f(_lightAt, camera.Light);
        gl.Uniform1f(_ambientAt, HullRasteriser.Ambient);
        _uniform4f!(_hullAt, hull.X, hull.Y, hull.Z, hull.W);
        _uniform4f(_backgroundAt, background.X, background.Y, background.Z, background.W);

        gl.BindVertexArray(_vertexArray);
        gl.DrawElements(Triangles, _indexCount, UnsignedInt, IntPtr.Zero);
        gl.BindVertexArray(0);

        gl.BindFramebuffer(ReadFramebuffer, _framebuffer);
        gl.BindFramebuffer(DrawFramebuffer, fb);
        gl.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ColorBufferBit, Nearest);
        gl.BindFramebuffer(Framebuffer, fb);
        Drawn?.Invoke(gl, width, height);
    }

    private void Init(GlInterface gl)
    {
        if (!gl.IsBlitFramebufferAvailable || !gl.IsGenVertexArraysAvailable || !gl.IsBindVertexArrayAvailable)
        {
            throw new InvalidOperationException("the driver has no framebuffer blit or vertex arrays");
        }

        _storageMultisample = Proc<RenderbufferStorageMultisampleProc>(gl, "glRenderbufferStorageMultisample");
        _uniform3f = Proc<Uniform3fProc>(gl, "glUniform3f");
        _uniform4f = Proc<Uniform4fProc>(gl, "glUniform4f");
        _uniformMatrix4fv = Proc<UniformMatrix4fvProc>(gl, "glUniformMatrix4fv");

        var header = GlVersion.Type == GlProfileType.OpenGLES
            ? "#version 300 es\nprecision highp float;\n"
            : "#version 330 core\n";

        var vertex = Compile(gl, VertexShader, header + VertexSource);
        var fragment = Compile(gl, FragmentShader, header + FragmentSource);

        _program = gl.CreateProgram();
        gl.AttachShader(_program, vertex);
        gl.AttachShader(_program, fragment);
        gl.BindAttribLocationString(_program, 0, "position");
        gl.BindAttribLocationString(_program, 1, "normal");

        var linked = gl.LinkProgramAndGetError(_program);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);

        if (linked is not null)
        {
            throw new InvalidOperationException($"the hull shaders did not link: {linked}");
        }

        _mvpAt = gl.GetUniformLocationString(_program, "mvp");
        _viewAt = gl.GetUniformLocationString(_program, "view");
        _toLightAt = gl.GetUniformLocationString(_program, "toLight");
        _lightAt = gl.GetUniformLocationString(_program, "light");
        _ambientAt = gl.GetUniformLocationString(_program, "ambient");
        _hullAt = gl.GetUniformLocationString(_program, "hull");
        _backgroundAt = gl.GetUniformLocationString(_program, "background");

        Upload(gl, _pose.Mesh);
    }

    /// <summary>Position and normal interleaved, six floats a vertex; a vertex with no normal gets zero.</summary>
    private void Upload(GlInterface gl, HullMesh mesh)
    {
        var positions = mesh.Positions;
        var normals = mesh.Normals;
        var interleaved = new float[positions.Length * 6];

        for (var v = 0; v < positions.Length; v++)
        {
            var n = v < normals.Length ? normals[v] : Vector3.Zero;
            var at = v * 6;
            interleaved[at] = positions[v].X;
            interleaved[at + 1] = positions[v].Y;
            interleaved[at + 2] = positions[v].Z;
            interleaved[at + 3] = n.X;
            interleaved[at + 4] = n.Y;
            interleaved[at + 5] = n.Z;
        }

        _indexCount = mesh.Indices.Length - (mesh.Indices.Length % 3);

        _vertexArray = gl.GenVertexArray();
        gl.BindVertexArray(_vertexArray);

        _vertices = gl.GenBuffer();
        gl.BindBuffer(ArrayBuffer, _vertices);
        Buffer(gl, ArrayBuffer, interleaved, interleaved.Length * sizeof(float));

        _indices = gl.GenBuffer();
        gl.BindBuffer(ElementArrayBuffer, _indices);
        Buffer(gl, ElementArrayBuffer, mesh.Indices, _indexCount * sizeof(int));

        gl.VertexAttribPointer(0, 3, Float, 0, 6 * sizeof(float), IntPtr.Zero);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 3, Float, 0, 6 * sizeof(float), 3 * sizeof(float));
        gl.EnableVertexAttribArray(1);

        gl.BindVertexArray(0);
    }

    /// <summary>The multisampled colour and depth targets, remade when the size changes.</summary>
    private void Fit(GlInterface gl, int width, int height)
    {
        if (_framebuffer != 0 && width == _width && height == _height)
        {
            return;
        }

        DeleteTargets(gl);

        _framebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(Framebuffer, _framebuffer);

        _colour = gl.GenRenderbuffer();
        gl.BindRenderbuffer(Renderbuffer, _colour);
        _storageMultisample!(Renderbuffer, Samples, Rgba8, width, height);
        gl.FramebufferRenderbuffer(Framebuffer, ColorAttachment0, Renderbuffer, _colour);

        _depth = gl.GenRenderbuffer();
        gl.BindRenderbuffer(Renderbuffer, _depth);
        _storageMultisample(Renderbuffer, Samples, DepthComponent24, width, height);
        gl.FramebufferRenderbuffer(Framebuffer, DepthAttachment, Renderbuffer, _depth);

        gl.BindRenderbuffer(Renderbuffer, 0);

        var status = gl.CheckFramebufferStatus(Framebuffer);

        if (status != FramebufferComplete)
        {
            DeleteTargets(gl);
            throw new InvalidOperationException($"the {Samples}× multisampled framebuffer is incomplete (status 0x{status:X})");
        }

        _width = width;
        _height = height;
    }

    private void DeleteTargets(GlInterface gl)
    {
        if (_framebuffer != 0)
        {
            gl.DeleteFramebuffer(_framebuffer);
        }

        if (_colour != 0)
        {
            gl.DeleteRenderbuffer(_colour);
        }

        if (_depth != 0)
        {
            gl.DeleteRenderbuffer(_depth);
        }

        _framebuffer = _colour = _depth = _width = _height = 0;
    }

    private void Fail(string reason)
    {
        if (_failed)
        {
            return;
        }

        _failed = true;
        Failed?.Invoke(reason);
    }

    private static int Compile(GlInterface gl, int kind, string source)
    {
        var shader = gl.CreateShader(kind);

        if (gl.CompileShaderAndGetError(shader, source) is { } error)
        {
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"a hull shader did not compile: {error}");
        }

        return shader;
    }

    private static T Proc<T>(GlInterface gl, string name)
        where T : Delegate
    {
        var address = gl.GetProcAddress(name);

        return address == IntPtr.Zero
            ? throw new InvalidOperationException($"the driver has no {name}")
            : Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private static void Buffer(GlInterface gl, int target, Array data, int bytes)
    {
        var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);

        try
        {
            gl.BufferData(target, bytes, pinned.AddrOfPinnedObject(), StaticDraw);
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <summary>Row-major, which OpenGL reads as the transpose: what a column-vector shader needs.</summary>
    private static float[] Floats(Matrix4x4 m) =>
    [
        m.M11, m.M12, m.M13, m.M14,
        m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34,
        m.M41, m.M42, m.M43, m.M44,
    ];

    /// <summary>A premultiplied ARGB colour as red, green, blue and alpha, 0 to 1.</summary>
    private static Vector4 Channels(uint argb) =>
        new(((argb >> 16) & 0xFF) / 255f, ((argb >> 8) & 0xFF) / 255f, (argb & 0xFF) / 255f, (argb >> 24) / 255f);
}
