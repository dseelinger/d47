using Avalonia.Controls;

namespace D47.App.Headset;

/// <summary>
/// The caption quad's only content. No code behind it beyond the constructor: a caption is
/// output, it takes no input, and everything about when it appears and how long it stays
/// belongs to <see cref="D47.Core.Vr.CaptionLayer"/>.
/// </summary>
public partial class CaptionView : UserControl
{
    public CaptionView() => InitializeComponent();
}
