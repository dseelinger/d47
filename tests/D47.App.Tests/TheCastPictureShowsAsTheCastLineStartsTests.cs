using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Voice;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A story's Narrator line then a cast line, spoken through the real chain from the Announcer to the arbiter to every
/// avatar: when the cast line starts each avatar shows the member, and when it ends the core (#2229, #1139).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheCastPictureShowsAsTheCastLineStartsTests : IDisposable
{
    private const string Member = "the-test-story.ren";

    private static readonly Color NarratorBlue = Color.FromRgb(0, 0, 255);
    private static readonly Color MemberRed = Color.FromRgb(255, 0, 0);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "d47-cast-chain-" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _output;

    public TheCastPictureShowsAsTheCastLineStartsTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [AvaloniaFact]
    public void InOneGroupTheMembersPictureIsUpAsTheirLineStarts() => Run(separateGroups: false);

    [AvaloniaFact]
    public void AfterTheGapBetweenGroupsTheMembersPictureIsUpAsTheirLineStarts() => Run(separateGroups: true);

    /// <summary>Ticks served at a point where the avatar shows the core: enough for its clip's quad to go up over the panel.</summary>
    private const int Settled = 4;

    private void Run(bool separateGroups)
    {
        var chain = new Chain(_root);
        var label = separateGroups ? "groups" : "group";

        var idle = chain.Record("idle", label, Settled);

        // Synthesised and queued ahead of playback, as the Announcer's speaking loop does.
        Task.Run(async () =>
        {
            await chain.Announcer.SayAsync(NarratorLine(separateGroups));
            await chain.Announcer.SayAsync(CastLine());
        }).Wait(TestContext.Current.CancellationToken);

        // Each start is recorded on the first tick after it, which is when the headset must already have changed.
        var narratorStart = chain.Record("narrator-start", label);

        chain.FinishLine();

        Point? gap = null;

        if (separateGroups)
        {
            gap = chain.Record("gap", label, Settled);
            chain.FinishLine();
        }

        var castStart = chain.Record("cast-start", label);

        chain.FinishLine();

        var castFinish = chain.Record("cast-finish", label);
        var coreAgain = chain.Record("core-again", label, Settled);

        _output.WriteLine(chain.Recording());

        AssertTheCoreShows(idle);

        Assert.Equal(SpeakerPictures.Narrator, narratorStart.Activity);
        AssertEverySurfaceShows(narratorStart, SpeakerPictures.Narrator, "narrator");

        if (gap is not null)
        {
            Assert.Equal(AudioChannel.Cue, gap.Channel);
            AssertTheCoreShows(gap);
        }

        Assert.Equal([SpeakerPictures.Narrator, Member, Member], chain.Requested);
        Assert.Equal(Member, castStart.Activity);
        Assert.Equal(Member, castStart.SpeakingPicture);
        AssertEverySurfaceShows(castStart, Member, "member");

        Assert.Null(castFinish.Activity);
        Assert.Null(castFinish.SpeakingPicture);

        foreach (var surface in castFinish.Surfaces)
        {
            Assert.True(surface.Speaker is null, $"{surface.Name} still shows {surface.Speaker} once the cast line ended");
            Assert.True(surface.ShowingClip, $"{surface.Name} shows no core clip once the cast line ended");
        }

        AssertTheCoreShows(coreAgain);

        chain.Close();
    }

    /// <summary>Every avatar plays the core's clip, and in the headset the clip's quad is up over the panel.</summary>
    private static void AssertTheCoreShows(Point point)
    {
        Assert.Null(point.SpeakingPicture);

        foreach (var surface in point.Surfaces)
        {
            Assert.True(surface.Speaker is null, $"{surface.Name} shows {surface.Speaker} at {point.Name}, not the core");
            Assert.True(surface.ShowingClip, $"{surface.Name} shows no core clip at {point.Name}");

            if (surface.Quad is { } quad)
            {
                Assert.True(quad, $"{surface.Name}'s avatar quad is not up over the core clip at {point.Name}");
            }
        }
    }

    private static void AssertEverySurfaceShows(Point point, string picture, string colour)
    {
        foreach (var surface in point.Surfaces)
        {
            Assert.True(surface.Speaker == picture, $"{surface.Name} shows {surface.Speaker ?? "the core"} at {point.Name}, not {picture}");
            Assert.False(surface.ShowingClip, $"{surface.Name} shows a core clip at {point.Name}");
            Assert.True(surface.Drawn == colour, $"{surface.Name} drew {surface.Drawn} at {point.Name}, not {colour}");

            if (surface.Quad is { } quad)
            {
                Assert.False(quad, $"{surface.Name}'s avatar quad covers the panel at {point.Name}");
            }
        }
    }

    /// <summary>A Narrator line; in another group than the cast line when asked, so the arbiter puts its gap between them.</summary>
    private static Announcement NarratorLine(bool separateGroups) =>
        new(separateGroups ? NpcChatter.LineKey : $"{D47.Core.Adventures.AdventureCallout.KeyPrefix}chapter-one.0", "The yard lights stay on.")
        {
            Voice = VoiceRole.Narrator,
        };

    /// <summary>A cast member's line, as <c>LineWriter.Voiced</c> writes it.</summary>
    private static Announcement CastLine() =>
        new($"{D47.Core.Adventures.AdventureCallout.LinePrefix}chapter-one.0.1", "Bay four. Sign here.")
        {
            Voice = VoiceRole.Crew,
            Speaker = "Ren",
            Cast = "ren",
            Pinned = new PinnedVoice(StorySpeaker.Kokoro, "bm_george"),
            Picture = Member,
        };

    /// <summary>What one surface showed at one point.</summary>
    private sealed record Shown(string Name, string? Speaker, bool ShowingClip, bool? Dirty, string Drawn, bool? Quad);

    /// <summary>Every hop at one point.</summary>
    private sealed record Point(string Name, AudioChannel? Channel, string? Activity, string? SpeakingPicture, IReadOnlyList<Shown> Surfaces);

    /// <summary>The Announcer, the voice pipeline and a real arbiter on a hand-finished sink, feeding four surfaces of one model.</summary>
    private sealed class Chain
    {
        private readonly HandDrivenSink _sink = new();
        private readonly AudioArbiter _arbiter;
        private readonly SpeakerPortraits _portraits;
        private readonly PanelViewModel _model = new() { CoreId = "covas" };
        private readonly List<(string Name, Window Window, PanelView View)> _desktop = [];
        private readonly List<(string Name, VrPanelSurface Panel, VrAvatarSurface Avatar, PanelView View)> _headset = [];
        private readonly List<Point> _points = [];
        private readonly Dictionary<VrPanelSurface, byte[]> _drawn = [];
        private DateTimeOffset _now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

        internal Chain(string root)
        {
            var paths = new AppPaths(root, Path.Combine(root, "build"));
            Directory.CreateDirectory(paths.AvatarClips);

            foreach (var state in Enum.GetValues<LoopState>())
            {
                File.Copy(HeadsetAvatarRig.Fixture(), Path.Combine(paths.AvatarClips, CoreClips.FileName("covas", state)), overwrite: true);
            }

            var files = new MemoryFileSystem();
            files.WriteBytes(Path.Combine(paths.ShippedPortraits, SpeakerPictures.Narrator + ".jpg"), Solid(NarratorBlue));
            files.WriteBytes(Path.Combine(paths.Stories, Member + ".jpg"), Solid(MemberRed));
            _portraits = new SpeakerPortraits(new SpeakerPictures(files, paths));

            var avatars = AvatarLibrary.Load(new DiskFileSystem(), paths);

            _arbiter = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
            _arbiter.Played += request =>
            {
                if (request.Channel == AudioChannel.Speech)
                {
                    Requested.Add(request.Picture);
                }
            };

            // What AppHost does.
            _arbiter.ActivityChanged += _ => Dispatcher.UIThread.Post(() => _model.SpeakingPicture = _arbiter.Activity.Picture);

            var tone = new OneToneProvider();
            var voice = new VoicePipeline(_arbiter, CueLibrary.Load, NullLoggerFactory.Instance)
            {
                Tts = tone,
                SpeakerFor = _ => tone,
                PinnedFor = _ => tone,
                Voice = new VoiceSelection("one-tone"),
                CuesEnabled = false,
                BedEnabled = false,
            };

            var settings = TestSurface.Create().Settings;
            var cast = new VoiceCast { Pool = ["one-tone"] };

            // SayAsync reaches none of the turn loop, the writer or the records for a line with no ConversationLine.
            Announcer = new Announcer(
                voice,
                _arbiter,
                CueLibrary.Load,
                turns: null!,
                new PersonaHost(),
                new GameStateStore(),
                () => NavRoute.None,
                NullLoggerFactory.Instance,
                _ => cast,
                _ => D47.Core.Audio.VoiceGender.Unlabelled,
                _ => { },
                settings,
                new SpendTracker(),
                new CalloutEngine(NullLogger<CalloutEngine>.Instance),
                writer: null!,
                records: null!,
                _ => { },
                () => false);

            Desktop("main-window", PanelMode.Full, 900, 700, avatars);
            Desktop("desktop-mini", PanelMode.Mini, 792, 280, avatars);
            Headset("vr-mini", "mini", avatars);
            Headset("vr-full", "full", avatars);

            Serve();
        }

        internal Announcer Announcer { get; }

        /// <summary>The picture on each speech request the arbiter played to its end, in order: one per sentence.</summary>
        internal List<string?> Requested { get; } = [];

        /// <summary>Finishes what is playing, sentence by sentence, until something else plays or nothing does.</summary>
        internal void FinishLine()
        {
            var line = _arbiter.Activity;
            Assert.True(line.Utterance is not null, "nothing is playing to finish");

            do
            {
                _sink.Finish(_arbiter.Activity.Utterance!.Value);
            }
            while (_arbiter.Activity is { Utterance: not null } next && next.Channel == line.Channel && next.Picture == line.Picture);
        }

        /// <summary>Runs the posted work, serves the headset <paramref name="ticks"/> times, and records and captures every hop on the last.</summary>
        internal Point Record(string name, string label, int ticks = 1)
        {
            var activity = _arbiter.Activity;

            for (var tick = 1; tick < ticks; tick++)
            {
                Serve();
            }

            Dispatcher.UIThread.RunJobs();

            var shown = new List<Shown>();

            foreach (var (surface, window, view) in _desktop)
            {
                using var frame = window.CaptureRenderedFrame()!;
                var pixels = PixelsOf(frame);
                shown.Add(new Shown(
                    surface, SpeakerOf(view.Avatar), view.Avatar.ShowingClip is not null, null,
                    Classify(pixels, frame.PixelSize.Width, SamplesOf(view.Avatar), rgba: frame.Format == PixelFormats.Rgba8888), null));
                frame.SaveCapture($"{label}-{name}-{surface}.png");
            }

            foreach (var (surface, panel, avatar, view) in _headset)
            {
                var dirty = panel.IsDirty;
                var (frame, width, height) = ServeOne(panel, avatar);
                var drawn = Classify(frame, width, SamplesOf(view.Avatar));

                shown.Add(new Shown(surface, SpeakerOf(view.Avatar), view.Avatar.ShowingClip is not null, dirty, drawn, avatar.Visible));
                SavePixels(frame, width, height, $"{label}-{name}-{surface}.png");
            }

            var point = new Point(name, activity.Channel, activity.Picture, _model.SpeakingPicture, shown);
            _points.Add(point);

            return point;
        }

        internal string Recording()
        {
            var text = new StringBuilder();

            foreach (var point in _points)
            {
                text.AppendLine($"{point.Name}: channel {point.Channel?.ToString() ?? "none"}, Activity.Picture {point.Activity ?? "null"}, SpeakingPicture {point.SpeakingPicture ?? "null"}");

                foreach (var surface in point.Surfaces)
                {
                    text.AppendLine(
                        $"  {surface.Name}: Speaker {surface.Speaker ?? "null"}, ShowingClip {(surface.ShowingClip ? "core clip" : "null")}"
                        + (surface.Dirty is { } dirty ? $", IsDirty {dirty}" : string.Empty)
                        + $", drawn {surface.Drawn}"
                        + (surface.Quad is { } quad ? $", avatar quad {(quad ? "visible" : "hidden")}" : string.Empty));
                }
            }

            text.AppendLine($"requests played: {string.Join(", ", Requested.Select(picture => picture ?? "null"))}");

            return text.ToString();
        }

        internal void Close()
        {
            foreach (var (_, window, _) in _desktop)
            {
                window.Close();
            }

            foreach (var (_, panel, avatar, _) in _headset)
            {
                avatar.Dispose();
                panel.Dispose();
            }

            _arbiter.Dispose();
        }

        private void Desktop(string name, PanelMode mode, double width, double height, AvatarLibrary avatars)
        {
            var view = new PanelView { DataContext = _model, Mode = mode };
            view.EnableSpeakerPictures(_portraits);
            view.Avatar.Library = avatars;

            var window = new Window { Content = view, Width = width, Height = height };
            window.Show();

            var bounds = new Rect(0, 0, width, height);
            window.Measure(bounds.Size);
            window.Arrange(bounds);

            _desktop.Add((name, window, view));
        }

        private void Headset(string name, string mode, AvatarLibrary avatars)
        {
            var (settings, _, _) = TestSurface.Create();

            settings.Replace(
                "test",
                current => current with
                {
                    Vr = current.Vr with
                    {
                        Mode = mode,
                        Panel = current.Vr.Panel with { Pixels = "1250x690", Zoom = 80 },
                        Mini = current.Vr.Mini with { Pixels = "792x280", Zoom = 100 },
                    },
                });

            var panel = new VrPanelSurface(_model, settings, _ => null, avatars: avatars, portraits: _portraits) { Enabled = true };
            var view = (PanelView)typeof(VrPanelSurface)
                .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(panel)!;

            _headset.Add((name, panel, new VrAvatarSurface(panel), view));
        }

        private void Serve()
        {
            Dispatcher.UIThread.RunJobs();

            foreach (var (_, panel, avatar, _) in _headset)
            {
                ServeOne(panel, avatar);
            }
        }

        /// <summary>
        /// One tick of the headset as <c>VrHost.Serve</c> and <c>SteamVrRuntime</c> run it: the avatar advances, the panel
        /// is drawn only when dirty, and the avatar's quad is laid over what the panel last drew while it is up.
        /// </summary>
        private (byte[] Frame, int Width, int Height) ServeOne(VrPanelSurface panel, VrAvatarSurface avatar)
        {
            _now = _now.AddMilliseconds(100);

            panel.Configure();
            panel.ApplyMode();
            Dispatcher.UIThread.RunJobs();

            avatar.Advance(_now);

            var (width, height) = panel.Size;

            if (panel.IsDirty || !_drawn.TryGetValue(panel, out var drawn) || drawn.Length != width * height * 4)
            {
                drawn = new byte[width * height * 4];

                unsafe
                {
                    fixed (byte* pixels = drawn)
                    {
                        panel.Draw((IntPtr)pixels, width * 4);
                    }
                }

                _drawn[panel] = drawn;
            }

            var frame = (byte[])drawn.Clone();

            if (avatar.Visible && panel.AvatarArea is { } area)
            {
                var (quadWidth, quadHeight) = avatar.Size;
                var quad = new byte[quadWidth * quadHeight * 4];

                unsafe
                {
                    fixed (byte* pixels = quad)
                    {
                        avatar.Draw((IntPtr)pixels, quadWidth * 4);
                    }
                }

                LayOver(frame, width, height, quad, quadWidth, quadHeight, area);
            }

            return (frame, width, height);
        }

        /// <summary>The picture name the avatar shows, or null for the core.</summary>
        private string? SpeakerOf(AvatarView avatar) =>
            avatar.Speaker is not { } speaker ? null
            : ReferenceEquals(speaker, _portraits.For(Member, (int)(PanelView.HeaderAvatarExtent * 2))) ? Member
            : ReferenceEquals(speaker, _portraits.For(SpeakerPictures.Narrator, (int)(PanelView.HeaderAvatarExtent * 2))) ? SpeakerPictures.Narrator
            : "another picture";

        /// <summary>The avatar's centre and four points a quarter of its side from it, in the frame's pixels.</summary>
        private static PixelPoint[] SamplesOf(AvatarView avatar)
        {
            Visual root = TopLevel.GetTopLevel(avatar)!;
            var (width, height) = (avatar.Bounds.Width, avatar.Bounds.Height);

            return [.. new[] { (0.5, 0.5), (0.25, 0.25), (0.75, 0.25), (0.25, 0.75), (0.75, 0.75) }.Select(at =>
            {
                var point = avatar.TranslatePoint(new Avalonia.Point(width * at.Item1, height * at.Item2), root)!.Value;
                return new PixelPoint((int)point.X, (int)point.Y);
            })];
        }

        /// <summary>Which picture fills every sample: the member's red, the Narrator's blue, or neither, which is the core.</summary>
        private static string Classify(byte[] pixels, int width, PixelPoint[] samples, bool rgba = false)
        {
            var colours = samples.Select(at => ColourAt(pixels, width, at, rgba)).Distinct().ToList();

            return colours.Count == 1 ? colours[0] : "core";
        }

        private static string ColourAt(byte[] pixels, int width, PixelPoint at, bool rgba)
        {
            var index = ((at.Y * width) + at.X) * 4;

            if (index < 0 || index + 3 >= pixels.Length)
            {
                return "outside the frame";
            }

            var (first, green, third) = (pixels[index], pixels[index + 1], pixels[index + 2]);
            var (red, blue) = rgba ? (first, third) : (third, first);

            return red > 180 && green < 80 && blue < 80 ? "member"
                : blue > 180 && green < 80 && red < 80 ? "narrator"
                : "core";
        }

        private static void LayOver(byte[] frame, int width, int height, byte[] quad, int quadWidth, int quadHeight, VrPixelRect area)
        {
            for (var y = 0; y < (int)area.Height; y++)
            {
                var row = (int)area.Y + y;

                if (row < 0 || row >= height)
                {
                    continue;
                }

                var from = Math.Min(quadHeight - 1, y * quadHeight / Math.Max(1, (int)area.Height));

                for (var x = 0; x < (int)area.Width; x++)
                {
                    var column = (int)area.X + x;

                    if (column < 0 || column >= width)
                    {
                        continue;
                    }

                    var source = ((from * quadWidth) + Math.Min(quadWidth - 1, x * quadWidth / Math.Max(1, (int)area.Width))) * 4;
                    Array.Copy(quad, source, frame, ((row * width) + column) * 4, 4);
                }
            }
        }

        private static byte[] PixelsOf(Bitmap frame)
        {
            var stride = frame.PixelSize.Width * 4;
            var pixels = new byte[stride * frame.PixelSize.Height];

            unsafe
            {
                fixed (byte* buffer = pixels)
                {
                    frame.CopyPixels(new PixelRect(0, 0, frame.PixelSize.Width, frame.PixelSize.Height), (IntPtr)buffer, pixels.Length, stride);
                }
            }

            return pixels;
        }

        private static void SavePixels(byte[] bgra, int width, int height, string fileName)
        {
            using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

            using (var locked = bitmap.Lock())
            {
                for (var row = 0; row < height; row++)
                {
                    Marshal.Copy(bgra, row * width * 4, locked.Address + (row * locked.RowBytes), width * 4);
                }
            }

            bitmap.SaveCapture(fileName);
        }

        private static byte[] Solid(Color colour)
        {
            using var bitmap = new WriteableBitmap(new PixelSize(64, 64), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

            using (var buffer = bitmap.Lock())
            {
                var row = new byte[buffer.RowBytes];

                for (var x = 0; x < 64; x++)
                {
                    row[(x * 4) + 0] = colour.B;
                    row[(x * 4) + 1] = colour.G;
                    row[(x * 4) + 2] = colour.R;
                    row[(x * 4) + 3] = 255;
                }

                for (var y = 0; y < 64; y++)
                {
                    Marshal.Copy(row, 0, buffer.Address + (y * buffer.RowBytes), row.Length);
                }
            }

            using var stream = new MemoryStream();
            bitmap.Save(stream, new JpegBitmapEncoderOptions());

            return stream.ToArray();
        }
    }
}
