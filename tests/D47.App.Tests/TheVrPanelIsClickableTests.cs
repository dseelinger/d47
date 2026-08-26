using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Pressing a control on the panel from inside the headset
/// (remediation.md, "Controls should be clickable in the VR panels").
/// <para>
/// The surface is rasterised out of a window that is never shown, so it receives no input from
/// the desktop at all. What reaches it is a fraction across a quad, from a controller — turned
/// into the routed events every control already understands, so a tab selects and a button
/// presses without either of them knowing where the press came from.
/// </para>
/// </summary>
public class TheVrPanelIsClickableTests
{
    private static readonly PixelSize Quad = new(1024, 640);

    /// <summary>The centre of a control, in the surface's own coordinates.</summary>
    private static Point Centre(Control view, Control target)
    {
        var at = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), view);
        Assert.NotNull(at);
        return at.Value;
    }

    private static Control Tab(PanelView view, string name) =>
        view.GetVisualDescendants().OfType<RadioButton>().First(tab => (tab.Content as string) == name);

    /// <summary>
    /// The control carrying a word, whatever kind of control it is. The readings of a page are
    /// answers in a chooser now rather than segments in a strip (remediation.md 10, item 1), and
    /// what this file is about is a ray landing on a thing and the thing responding — not which
    /// class the thing happens to be.
    /// </summary>
    private static Control Saying(PanelView view, string word) =>
        view.GetVisualDescendants()
            .OfType<Control>()
            .Last(control => control is Button or RadioButton
                             && control.GetVisualDescendants()
                                 .OfType<TextBlock>()
                                 .Any(text => text.Text == word));

    /// <summary>
    /// Opens the readings of this page the way a ray does: a press on the button that offers
    /// them. Rendered in between, because a control that has just appeared has no bounds until
    /// the next layout pass and a press aimed at it would land on whatever was there before.
    /// </summary>
    private static void OpenModes(PanelView view, OffscreenSurface surface)
    {
        surface.Render();
        Assert.True(surface.Click(Centre(view, view.GetControl<Button>("ModeButton"))), "the press landed on something");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        surface.Render();
    }

    [AvaloniaFact]
    public void PressingATabChangesThePage()
    {
        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        surface.Render();

        Assert.Equal(TranscriptPage.Conversation, view.Page);

        OpenModes(view, surface);

        Assert.True(surface.Click(Centre(view, Saying(view, "Details"))), "the press landed on something");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Technical, view.Page);

        OpenModes(view, surface);

        surface.Click(Centre(view, Saying(view, "Thread")));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Conversation, view.Page);
    }

    /// <summary>
    /// And the frame that comes back afterwards is the new page, which is the half a Commander
    /// in a headset can actually see.
    /// </summary>
    [AvaloniaFact]
    public void TheSurfaceRedrawsAsThePageChanges()
    {
        var model = new PanelViewModel();
        model.Append("The Commander and the ship's AI.");
        model.Append("[12:00:00] Microphone open, listening.\n", TranscriptKind.Technical);

        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var before = Frame(surface);

        OpenModes(view, surface);

        surface.Click(Centre(view, Saying(view, "Details")));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(before, Frame(surface));
    }

    /// <summary>
    /// A press on nothing in particular is a press on nothing in particular. The panel is mostly
    /// transcript, and a ray landing on it should not do anything at all.
    /// </summary>
    [AvaloniaFact]
    public void PressingTheTranscriptChangesNothing()
    {
        var model = new PanelViewModel();
        model.Append("Fixture One, docked.");

        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        surface.Click(new Point(Quad.Width / 2, Quad.Height / 2));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Conversation, view.Page);
    }

    private static string Frame(OffscreenSurface surface)
    {
        var rendered = surface.Render();
        var size = rendered.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];

        unsafe
        {
            fixed (byte* buffer = pixels)
            {
                rendered.CopyPixels(
                    new PixelRect(0, 0, size.Width, size.Height), (IntPtr)buffer, pixels.Length, stride);
            }
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels));
    }

    /// <summary>
    /// The jump-to-latest control, pressed from a headset
    /// (remediation.md, "The Newest button in VR does not appear to work").
    /// </summary>
    [AvaloniaFact]
    public void PressingJumpToLatestFollowsAgain()
    {
        var model = new PanelViewModel();

        for (var line = 0; line < 200; line++)
        {
            model.Append($"Line {line} of the transcript, long enough to wrap once or twice.\n");
        }

        var view = new PanelView { DataContext = model };
        using var surface = new OffscreenSurface(view, Quad);

        // Twice: the first pass is what gives the tree an extent, and following is asserted
        // against it on the pass after.
        surface.Render(view.KeepUp);
        surface.Render(view.KeepUp);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var scroller = view.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .First(found => found.Name == "TranscriptScroller");

        // The headset shows the newest lines, which is the half of this that was silently wrong:
        // it sat at the top of a 3,271-pixel transcript through a whole session.
        Assert.True(scroller.Offset.Y > 0, "the transcript follows the newest line");

        // Scrolled back through the log, which is when the control appears at all.
        scroller.Offset = new Vector(0, 0);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        surface.Render();

        var follow = view.GetVisualDescendants().OfType<Button>().First(found => found.Name == "FollowButton");

        Assert.True(follow.IsVisible, "the control is offered once the reader is behind");

        Assert.True(surface.Click(Centre(view, follow)), "the press landed on something");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        surface.Render(view.KeepUp);

        Assert.True(scroller.Offset.Y > 0, "it scrolled back to the newest line");
    }
}

/// <summary>
/// Controls that must not be pressed the ordinary way from a headset
/// (remediation.md 9, "clicking the Panel content drop-down caused the ray to get stuck").
/// <para>
/// A combo box opens a popup on the press, and a popup belongs to a top level — this one being a
/// window that is never shown. The reported result was a ray captured by a dropdown nobody could
/// see, and then a crash.
/// </para>
/// </summary>
public class PressingAControlThatOpensSomethingTests
{
    private static readonly PixelSize Quad = new(1024, 640);

    private static (Window Window, OffscreenSurface Surface, ComboBox Combo) WithACombo()
    {
        var combo = new ComboBox
        {
            ItemsSource = new[] { "full", "mini", "off" },
            SelectedIndex = 0,
            Width = 200,
            Height = 32,
        };

        var host = new Border { Child = combo, Width = Quad.Width, Height = Quad.Height };
        var surface = new OffscreenSurface(host, Quad);

        surface.Render();

        return (null!, surface, combo);
    }

    /// <summary>
    /// Pressing a combo box puts its list on the panel, and pressing a row chooses it.
    /// <para>
    /// The control's own dropdown is never opened. It is a popup, and a popup asks the platform
    /// for a top level of its own — this window has never been shown, so opening one recurses
    /// until the stack is gone and takes the process with it, at <c>0xC00000FD</c>, before any
    /// dispatcher work and with nothing in the log. That is the reported crash.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void PressingAComboBoxOffersItsListOnThePanel()
    {
        var (_, surface, combo) = WithACombo();
        using var _surface = surface;

        var at = combo.TranslatePoint(new Point(combo.Bounds.Width / 2, combo.Bounds.Height / 2), surface.View);
        Assert.NotNull(at);

        Assert.True(surface.Click(at.Value), "the press landed on the box");

        Assert.True(surface.IsChoosing, "the list is on the panel");
        Assert.False(combo.IsDropDownOpen, "and the control's own popup was never opened");

        surface.Render();

        // The rows are real controls in the real tree, which is what lets the same ray press them.
        var rows = surface.Root.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Content is "full" or "mini" or "off")
            .ToList();

        Assert.Equal(3, rows.Count);

        var row = rows[1];
        var onRow = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), surface.View);
        Assert.NotNull(onRow);

        Assert.True(surface.Click(onRow.Value), "the press landed on a row");

        Assert.Equal(1, combo.SelectedIndex);
        Assert.False(surface.IsChoosing, "and it put itself away");
    }

    /// <summary>Cancel closes it and changes nothing, which is the other half of a chooser.</summary>
    [AvaloniaFact]
    public void TheChooserCanBeLeftWithoutChoosing()
    {
        var (_, surface, combo) = WithACombo();
        using var _surface = surface;

        var at = combo.TranslatePoint(new Point(combo.Bounds.Width / 2, combo.Bounds.Height / 2), surface.View);
        Assert.NotNull(at);

        surface.Click(at.Value);
        surface.Render();

        var cancel = surface.Root.GetVisualDescendants()
            .OfType<Button>()
            .First(button => button.Content is "Cancel");

        var onCancel = cancel.TranslatePoint(new Point(cancel.Bounds.Width / 2, cancel.Bounds.Height / 2), surface.View);
        Assert.NotNull(onCancel);

        surface.Click(onCancel.Value);

        Assert.False(surface.IsChoosing);
        Assert.Equal(0, combo.SelectedIndex);
    }

    /// <summary>
    /// And a control that opens a window is left alone entirely — a dialog on a desktop the
    /// Commander is not looking at is a dialog they cannot answer.
    /// </summary>
    [AvaloniaFact]
    public void AControlMarkedDesktopOnlyIsNotPressed()
    {
        var pressed = 0;
        var button = new Button { Content = "Choose", Width = 200, Height = 32 };
        button.Classes.Add(OffscreenSurface.DesktopOnly);
        button.Click += (_, _) => pressed++;

        var host = new Border { Child = button, Width = Quad.Width, Height = Quad.Height };
        using var surface = new OffscreenSurface(host, Quad);

        surface.Render();

        var at = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), surface.View);
        Assert.NotNull(at);

        Assert.False(surface.Click(at.Value), "the press is refused rather than opening a window");
        Assert.Equal(0, pressed);
    }

    /// <summary>An ordinary button in the same host is still pressed, or this proves nothing.</summary>
    [AvaloniaFact]
    public void AnOrdinaryButtonStillIs()
    {
        var pressed = 0;
        var button = new Button { Content = "Copy", Width = 200, Height = 32 };
        button.Click += (_, _) => pressed++;

        var host = new Border { Child = button, Width = Quad.Width, Height = Quad.Height };
        using var surface = new OffscreenSurface(host, Quad);

        surface.Render();

        var at = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), surface.View);
        Assert.NotNull(at);

        Assert.True(surface.Click(at.Value));
        Assert.Equal(1, pressed);
    }
}

/// <summary>
/// Typing into a text box from a headset
/// (remediation.md 9, "all text boxes should be functional in VR").
/// <para>
/// There is no other way to fill one: the window is never shown, so it takes no keystrokes from
/// the desktop, and there is no keyboard in a cockpit to take them from anyway.
/// </para>
/// </summary>
public class TypingInVrTests
{
    private static readonly PixelSize Quad = new(1024, 640);

    private static (OffscreenSurface Surface, TextBox Box) WithABox(string text = "")
    {
        var box = new TextBox { Text = text, Width = 200, Height = 32 };
        var host = new Border { Child = box, Width = Quad.Width, Height = Quad.Height };
        var surface = new OffscreenSurface(host, Quad);

        surface.Render();

        return (surface, box);
    }

    private static void Press(OffscreenSurface surface, Control control)
    {
        var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), surface.View);
        Assert.NotNull(at);
        Assert.True(surface.Click(at.Value), $"the press landed on {control}");
    }

    private static Button Key(OffscreenSurface surface, string label) =>
        surface.Root.GetVisualDescendants().OfType<Button>().First(button => (button.Content as string) == label);

    [AvaloniaFact]
    public void PressingATextBoxOffersAKeyboard()
    {
        var (surface, box) = WithABox();
        using var _ = surface;

        Press(surface, box);

        Assert.True(surface.IsChoosing, "something is over the panel");

        surface.Render();

        // The keys are real buttons in the real tree, pressed by the same ray as everything else.
        Assert.NotNull(Key(surface, "a"));
        Assert.NotNull(Key(surface, "space"));
        Assert.NotNull(Key(surface, "Done"));
    }

    [AvaloniaFact]
    public void WhatIsTypedReachesTheBoxOnDone()
    {
        var (surface, box) = WithABox();
        using var _ = surface;

        Press(surface, box);
        surface.Render();

        foreach (var key in new[] { "s", "o", "l" })
        {
            Press(surface, Key(surface, key));
            surface.Render();
        }

        // Not yet: a settings row commits what it is handed, and committing letter by letter is
        // two wrong values on the way to the right one.
        Assert.Equal(string.Empty, box.Text);

        Press(surface, Key(surface, "Done"));

        Assert.Equal("sol", box.Text);
        Assert.False(surface.IsChoosing);
    }

    /// <summary>It starts from what is already in the box, so an edit is an edit.</summary>
    [AvaloniaFact]
    public void ItStartsFromWhatIsAlreadyThere()
    {
        var (surface, box) = WithABox("deciat");
        using var _ = surface;

        Press(surface, box);
        surface.Render();

        Press(surface, Key(surface, "delete"));
        surface.Render();
        Press(surface, Key(surface, "Done"));

        Assert.Equal("decia", box.Text);
    }

    [AvaloniaFact]
    public void CancelLeavesTheBoxAlone()
    {
        var (surface, box) = WithABox("deciat");
        using var _ = surface;

        Press(surface, box);
        surface.Render();

        Press(surface, Key(surface, "a"));
        surface.Render();
        Press(surface, Key(surface, "Cancel"));

        Assert.Equal("deciat", box.Text);
        Assert.False(surface.IsChoosing);
    }

    /// <summary>And clear empties it, for a row being retyped rather than corrected.</summary>
    [AvaloniaFact]
    public void ClearEmptiesIt()
    {
        var (surface, box) = WithABox("deciat");
        using var _ = surface;

        Press(surface, box);
        surface.Render();

        Press(surface, Key(surface, "clear"));
        surface.Render();
        Press(surface, Key(surface, "Done"));

        Assert.Equal(string.Empty, box.Text);
    }
}
