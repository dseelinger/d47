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

    /// <summary>Whether <see cref="Windowing.ModalHost"/> draws this dialog <see cref="Modal.TopOffset"/> below the top, not centred.</summary>
    public virtual bool AnchoredToTop => false;

    /// <summary>The share of the host's width the frame takes; null keeps the <see cref="Modal.Width"/> cap.</summary>
    public virtual double? WidthShare => null;
}
