using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace D47.App.Controls;

/// <summary>A dialog drawn inside a window rather than as one: a <see cref="DialogPage"/> or a <see cref="ModalDialog"/>.</summary>
public abstract class HostedDialog : UserControl
{
    private readonly TaskCompletionSource _left = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _opened;

    /// <summary>The title in the dialog's header.</summary>
    public string Title { get; protected set; } = string.Empty;

    /// <summary>Raised the first time the dialog is on screen.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised once, when the dialog has left its host.</summary>
    public event EventHandler? Closed;

    /// <summary>Completes when the dialog has left its host.</summary>
    public Task Left => _left.Task;

    /// <summary>What <see cref="Close"/> does while a host holds the dialog.</summary>
    internal Action? Leave { get; set; }

    protected IClipboard? Clipboard => TopLevel.GetTopLevel(this)?.Clipboard;

    protected IStorageProvider? StorageProvider => TopLevel.GetTopLevel(this)?.StorageProvider;

    /// <summary>Takes the dialog off its host.</summary>
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

    /// <summary>Called by the host once the dialog is off it.</summary>
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
