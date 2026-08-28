using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.Core.Audio;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>
/// The ship AI's face: one look per loop state, animated (Phase 11, "Ship's AI Avatar").
/// <para>
/// <b>Drawn rather than decoded, and that is the load-bearing decision.</b> One widget tree
/// renders to both the desktop window and the headset, so an animation made of decoded image
/// frames would need something advancing it on two surfaces — and it would need an image decoder
/// in a dependency graph that has stayed clean of them since Phase 1. Shapes and an Avalonia
/// animation are advanced by the framework on whichever surface they are rendered to, and the
/// theme repaints them without this class knowing a theme exists.
/// </para>
/// <para>
/// A Commander who drops their own frames into <c>data/avatar/&lt;state&gt;/</c> gets those
/// instead, for that state only — see <see cref="AvatarLibrary"/>. More than one file in the
/// folder cycles, which is how a genuinely animated format is supported without one being
/// decoded.
/// </para>
/// </summary>
public sealed class AvatarView : UserControl
{
    /// <summary>
    /// How long one frame of a dropped-in sequence holds. Not configurable, and deliberately
    /// slow: this sits on screen for the whole session and is not the thing being looked at.
    /// </summary>
    private static readonly TimeSpan FrameHold = TimeSpan.FromMilliseconds(250);

    private readonly Ellipse _ring = new();
    private readonly Path _core = new();
    private readonly Image _custom = new();
    private readonly Grid _root = new();

    private readonly DispatcherTimer _frames = new() { Interval = FrameHold };

    private AvatarLibrary? _library;
    private IReadOnlyList<Bitmap> _sequence = [];
    private int _frame;
    private LoopState _state = LoopState.Idle;

    public AvatarView()
    {
        Width = 44;
        Height = 44;

        _ring.StrokeThickness = 1.5;
        _ring.Opacity = 0.35;
        _ring.Width = 40;
        _ring.Height = 40;

        _core.Stretch = Stretch.Uniform;
        _core.Width = 22;
        _core.Height = 22;
        _core.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        _core.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

        _custom.Stretch = Stretch.Uniform;
        _custom.IsVisible = false;

        _root.Children.Add(_ring);
        _root.Children.Add(_core);
        _root.Children.Add(_custom);
        Content = _root;

        _frames.Tick += (_, _) => Advance();

        Apply(LoopState.Idle);
    }

    /// <summary>
    /// The Commander's own imagery, or null for none. Set once by the host after it has scanned
    /// the folders; a reload replaces it and the current state is re-applied.
    /// </summary>
    public AvatarLibrary? Library
    {
        get => _library;
        set
        {
            _library = value;
            Apply(_state);
        }
    }

    /// <summary>Which state to show. Idempotent: setting the current state again does nothing.</summary>
    public void Show(LoopState state)
    {
        if (state == _state)
        {
            return;
        }

        Apply(state);
    }

    private void Apply(LoopState state)
    {
        _state = state;

        StopSequence();

        if (LoadCustom(state) is { Count: > 0 } custom)
        {
            _sequence = custom;
            _frame = 0;
            _custom.Source = _sequence[0];
            _custom.IsVisible = true;
            _ring.IsVisible = false;
            _core.IsVisible = false;

            // One image is a still, not an animation. Starting a timer for it would wake the
            // dispatcher four times a second for the entire session to redraw the same pixels.
            if (_sequence.Count > 1)
            {
                _frames.Start();
            }

            return;
        }

        _custom.IsVisible = false;
        _custom.Source = null;
        _ring.IsVisible = true;
        _core.IsVisible = true;

        // Bound rather than looked up. A one-time TryFindResource in here resolves against a
        // control that is not attached to a tree yet — the panel sets the state while the view
        // is still being built — and quietly falls back to grey, which is what the first render
        // capture of this showed. A dynamic resource also repaints on a theme change without
        // this class knowing a theme exists, which is the rule every other view follows.
        var key = ResourceKeyFor(state);

        _ring.Bind(Shape.StrokeProperty, new DynamicResourceExtension(key));
        _core.Bind(Shape.FillProperty, new DynamicResourceExtension(key));
        _core.Data = GeometryFor(state);

        Animate(state);
    }

    /// <summary>
    /// Colour by role, never by literal, like every other view. Four roles carry eight states:
    /// muted for the quiet ones, the accent for anything in flight, danger for a failure and
    /// info for an explicit unsure — which is a state of the loop rather than an error and has
    /// never borrowed the failure colour anywhere else either.
    /// </summary>
    internal static string ResourceKeyFor(LoopState state) => state switch
    {
        LoopState.Failed => "D47.Danger",
        LoopState.Unsure => "D47.Info",
        LoopState.Idle => "D47.TextMuted",
        _ => "D47.Accent",
    };

    /// <summary>
    /// One distinctive centre per state, so they are told apart at a glance and in greyscale —
    /// colour is never the only signal. Authored here in a 24-unit space rather than lifted from
    /// an icon set, which keeps the licence graph as clean as the dependency graph, the same
    /// reasoning as the question mark already on this panel.
    /// </summary>
    private static Geometry GeometryFor(LoopState state) => Geometry.Parse(PathFor(state));

    /// <summary>
    /// The path data itself. Separate from the parse so a test can assert the eight are
    /// distinct — <see cref="Geometry.ToString"/> gives the type name, not the outline, so
    /// comparing parsed geometries silently compares nothing.
    /// </summary>
    internal static string PathFor(LoopState state) => state switch
    {
        // A closed ring: nothing in flight.
        LoopState.Idle => "M 12,4 A 8,8 0 1 1 11.99,4 Z M 12,8 A 4,4 0 1 0 12.01,8 Z",

        // An open mouth, facing up. The microphone is open.
        LoopState.Listening => "M 3,10 A 9,9 0 0 0 21,10 L 17,10 A 5,5 0 0 1 7,10 Z",

        // Three bars, left to right: sound becoming words.
        LoopState.Transcribing => "M 4,9 h 3 v 6 h -3 Z M 10.5,6 h 3 v 12 h -3 Z M 17,10 h 3 v 4 h -3 Z",

        // Four blocks around a gap — the Guardian motif, working.
        LoopState.Thinking => "M 4,4 h 6 v 6 h -6 Z M 14,4 h 6 v 6 h -6 Z M 4,14 h 6 v 6 h -6 Z M 14,14 h 6 v 6 h -6 Z",

        // A widening wave: speech leaving.
        LoopState.Speaking => "M 5,8 h 3 v 8 h -3 Z M 11,5 h 3 v 14 h -3 Z M 17,9 h 3 v 6 h -3 Z",

        // A tick.
        LoopState.Answered => "M 4,13 l 5,5 l 11,-13 l -3,-2 l -8,10 l -3,-3 Z",

        // A question, without a font in it.
        LoopState.Unsure =>
            "M 8,8 A 4,4 0 1 1 12,12 L 12,15 h -2 L 10,11 A 2.5,2.5 0 1 0 10,8 Z M 10,17 h 2 v 2 h -2 Z",

        // A cross.
        LoopState.Failed => "M 5,7 l 2,-2 l 5,5 l 5,-5 l 2,2 l -5,5 l 5,5 l -2,2 l -5,-5 l -5,5 l -2,-2 l 5,-5 Z",

        _ => "M 12,4 A 8,8 0 1 1 11.99,4 Z",
    };

    /// <summary>
    /// The movement. Every one of these is a slow opacity or scale cycle on one node, because
    /// this is on screen for the whole session and must not cost a frame — and because a
    /// companion's face that jitters is a companion that looks anxious.
    /// </summary>
    private void Animate(LoopState state)
    {
        _core.Transitions = null;
        _ring.Transitions = null;

        var (duration, from, to) = state switch
        {
            // Working. The only one that moves quickly enough to read as activity.
            LoopState.Thinking => (TimeSpan.FromMilliseconds(900), 0.45d, 1.0d),
            LoopState.Speaking => (TimeSpan.FromMilliseconds(500), 0.55d, 1.0d),
            LoopState.Listening => (TimeSpan.FromMilliseconds(1400), 0.6d, 1.0d),
            LoopState.Transcribing => (TimeSpan.FromMilliseconds(700), 0.5d, 1.0d),

            // At rest. A very slow breath, so the panel does not look frozen.
            _ => (TimeSpan.FromSeconds(4), 0.75d, 1.0d),
        };

        var pulse = new Animation
        {
            Duration = duration,
            IterationCount = IterationCount.Infinite,
            PlaybackDirection = PlaybackDirection.Alternate,
            Easing = new SineEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, from) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, to) } },
            },
        };

        // Fire and forget. The animation runs until the state changes and this is called again
        // with a different one; there is nothing to await and nothing to dispose.
        _ = pulse.RunAsync(_core);
    }

    private IReadOnlyList<Bitmap> LoadCustom(LoopState state)
    {
        if (_library?.For(state) is not { Count: > 0 } files)
        {
            return [];
        }

        var loaded = new List<Bitmap>(files.Count);

        foreach (var file in files)
        {
            try
            {
                loaded.Add(new Bitmap(file));
            }
            catch (Exception)
            {
                // A file that passed the readability check and is still a truncated PNG. The
                // library keeps unreadable files out; this keeps corrupt ones from breaking the
                // surface. Skipped rather than fatal — the other frames may be fine, and an
                // empty result falls back to the drawn face.
            }
        }

        return loaded;
    }

    private void Advance()
    {
        if (_sequence.Count == 0)
        {
            return;
        }

        _frame = (_frame + 1) % _sequence.Count;
        _custom.Source = _sequence[_frame];
    }

    private void StopSequence()
    {
        _frames.Stop();

        foreach (var bitmap in _sequence)
        {
            bitmap.Dispose();
        }

        _sequence = [];
        _frame = 0;
    }
}
