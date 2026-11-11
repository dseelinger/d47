using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>A genus row's species picture shown whole, with CODEX › beside CLOSE.</summary>
public sealed class SpeciesPictureDialog : ModalDialog
{
    public const string PictureName = "EnlargedSpeciesPicture";

    public const string CloseName = "CloseSpeciesPicture";

    public const string LikelyColour = "A likely colour. Not yet sampled here.";

    /// <summary>The picture's width: the dialog less its body inset.</summary>
    public const double PictureWidth = Modal.Width - 2 * Modal.Inset;

    public SpeciesPictureDialog(string genus, string bodyName, string species, Bitmap picture, bool isBodysOwnVariant, Button? codex)
    {
        Title = species;
        Width = Modal.Width;

        var image = new Image
        {
            Name = PictureName,
            Source = picture,
            Width = PictureWidth,
            Height = PictureWidth * picture.Size.Height / Math.Max(1, picture.Size.Width),
            Stretch = Stretch.Uniform,
        };
        var ground = new Border { Child = image, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        ThemeInk.Bind(ground, Border.BackgroundProperty, ThemeManager.SlabKey);

        var close = new Button { Name = CloseName, Content = "CLOSE", MinWidth = 76, Height = TypeScale.MinimumTarget };
        close.Click += (_, _) => Close();

        var buttons = new List<Control>();

        if (codex is not null)
        {
            codex.Click += (_, _) => Close();
            buttons.Add(codex);
        }

        buttons.Add(close);

        ThemeInk.Bind(this, TemplatedControl.BackgroundProperty, ThemeManager.BarKey);
        Content = Modal.Build(
            $"{genus} · {bodyName}",
            species,
            ground,
            buttons,
            subtitle: isBodysOwnVariant ? null : LikelyColour);

        Opened += (_, _) => close.Focus();
    }
}
