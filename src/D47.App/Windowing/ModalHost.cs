using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;

namespace D47.App.Windowing;

/// <summary>
/// The layer a window draws its <see cref="ModalDialog"/>s on: each one centred in a 1px A frame over the
/// scrim, the newest on top. Esc or a click on the scrim closes the top one, and focus stays inside it until
/// it closes (#521).
/// </summary>
public sealed class ModalHost : Grid
{
    public const string ScrimName = "ModalScrim";
    public const string FrameName = "ModalFrame";

    private readonly List<(ModalDialog Dialog, Border Scrim, IInputElement? Before)> _open = [];

    public ModalHost()
    {
        Name = "ModalHost";
        IsVisible = false;
        AddHandler(KeyDownEvent, OnKeyDown);
    }

    /// <summary>The modal on top, if one is open.</summary>
    public ModalDialog? Top => _open.Count == 0 ? null : _open[^1].Dialog;

    /// <summary>The host inside <paramref name="owner"/>, laid over its content when it has none yet.</summary>
    public static ModalHost For(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (owner.GetVisualDescendants().OfType<ModalHost>().FirstOrDefault() is { } found)
        {
            return found;
        }

        var host = new ModalHost();
        var content = owner.Content as Control;
        owner.Content = null;

        var layers = new Grid();

        if (content is not null)
        {
            layers.Children.Add(content);
        }

        layers.Children.Add(host);
        owner.Content = layers;

        return host;
    }

    /// <summary>Shows <paramref name="dialog"/> on top; completes when it closes.</summary>
    public Task Show(ModalDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        dialog.MaxWidth = Modal.Width;

        var frame = new Border
        {
            Name = FrameName,
            BorderThickness = new Thickness(1),
            MaxWidth = Modal.Width + 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = dialog,
        };
        Themed(frame, Border.BorderBrushProperty, ThemeManager.AKey);
        Themed(frame, Border.BackgroundProperty, ThemeManager.BarKey);

        var scrim = new Border { Name = ScrimName, Padding = new Thickness(24), Child = frame };
        Themed(scrim, Border.BackgroundProperty, ThemeManager.ScrimKey);
        KeyboardNavigation.SetTabNavigation(scrim, KeyboardNavigationMode.Cycle);

        scrim.PointerPressed += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, scrim))
            {
                return;
            }

            e.Handled = true;
            dialog.Close();
        };

        var before = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

        _open.Add((dialog, scrim, before));
        dialog.Leave = () => Remove(dialog);
        Children.Add(scrim);
        IsVisible = true;

        Dispatcher.UIThread.Post(() => FocusInto(scrim), DispatcherPriority.Loaded);

        return dialog.Left;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        TopLevel.GetTopLevel(this)?.AddHandler(GotFocusEvent, OnFocusMoved, handledEventsToo: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        TopLevel.GetTopLevel(this)?.RemoveHandler(GotFocusEvent, OnFocusMoved);

        base.OnDetachedFromVisualTree(e);
    }

    private void Remove(ModalDialog dialog)
    {
        var index = _open.FindIndex(open => ReferenceEquals(open.Dialog, dialog));

        if (index < 0)
        {
            return;
        }

        var (_, scrim, before) = _open[index];

        _open.RemoveAt(index);
        Children.Remove(scrim);
        IsVisible = _open.Count > 0;

        dialog.Release();

        if (index == _open.Count)
        {
            before?.Focus();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled || Top is not { } top)
        {
            return;
        }

        e.Handled = true;
        top.Close();
    }

    /// <summary>Puts focus back inside the top modal when something outside it takes it.</summary>
    private void OnFocusMoved(object? sender, RoutedEventArgs e)
    {
        if (_open.Count == 0 || e.Source is not Visual moved)
        {
            return;
        }

        var scrim = _open[^1].Scrim;

        if (!ReferenceEquals(moved, scrim) && !scrim.IsVisualAncestorOf(moved))
        {
            FocusInto(scrim);
        }
    }

    /// <summary>Focuses the modal itself unless something inside it already has focus.</summary>
    private static void FocusInto(Border scrim)
    {
        if (TopLevel.GetTopLevel(scrim)?.FocusManager?.GetFocusedElement() is Visual focused
            && scrim.IsVisualAncestorOf(focused))
        {
            return;
        }

        if (scrim.GetVisualDescendants().OfType<ModalDialog>().FirstOrDefault() is { } dialog)
        {
            dialog.Focus();
        }
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
