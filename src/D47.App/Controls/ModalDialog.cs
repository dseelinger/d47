using Avalonia.Controls;

namespace D47.App.Controls;

/// <summary>
/// A dialog of at most <see cref="Modal.Width"/> drawn over a scrim inside the window that opened it,
/// shown with <see cref="Windowing.Dialogs.Over(ModalDialog, Window)"/> (#521).
/// </summary>
public abstract class ModalDialog : HostedDialog
{
    protected ModalDialog()
    {
        Focusable = true;
    }
}
