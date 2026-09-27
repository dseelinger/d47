using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace D47.App.Controls;

/// <summary>
/// A dialog drawn as a page of the panel, opened with <see cref="Panel.PanelView.Open"/> and left
/// through its breadcrumb (#529).
/// </summary>
public abstract class DialogPage : UserControl
{
    private readonly TaskCompletionSource _left = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _opened;

    /// <summary>The title in the page's header.</summary>
    public string Title { get; protected set; } = string.Empty;

    /// <summary>The breadcrumb's word for this page.</summary>
    public abstract string Crumb { get; }

    /// <summary>Raised the first time the page is on screen.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised once, when the page has left the panel.</summary>
    public event EventHandler? Closed;

    /// <summary>Completes when the page has left the panel.</summary>
    public Task Left => _left.Task;

    /// <summary>What <see cref="Close"/> does while a panel holds the page: takes its crumb off the trail.</summary>
    internal Action? Leave { get; set; }

    protected IClipboard? Clipboard => TopLevel.GetTopLevel(this)?.Clipboard;

    protected IStorageProvider? StorageProvider => TopLevel.GetTopLevel(this)?.StorageProvider;

    /// <summary>Leaves the page, as its breadcrumb would.</summary>
    public void Close()
    {
        if (Leave is { } leave)
        {
            leave();
        }
        else
        {
            Release();
        }
    }

    /// <summary>Called by the panel once the page's crumb is off the trail.</summary>
    internal void Release()
    {
        if (_left.Task.IsCompleted)
        {
            return;
        }

        Leave = null;
        OnClosed(EventArgs.Empty);
        _left.SetResult();
    }

    protected virtual void OnOpened(EventArgs e) => Opened?.Invoke(this, e);

    protected virtual void OnClosed(EventArgs e) => Closed?.Invoke(this, e);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!_opened)
        {
            _opened = true;
            OnOpened(EventArgs.Empty);
        }
    }
}
