using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>
/// A picture kept in <c>data\pictures</c>, drawn at most <c>size</c> pixels wide, with Change picture and Use the
/// default buttons and a status line. With no picture name the buttons are disabled and the status says
/// <c>unsetStatus</c>.
/// </summary>
public sealed class PictureChooser : StackPanel
{
    private readonly SpeakerPictures _pictures;
    private readonly string? _picture;
    private readonly double _size;
    private readonly Control[] _beside;
    private readonly string? _unsetStatus;

    /// <summary><paramref name="beside"/> controls are placed in the buttons' row.</summary>
    public PictureChooser(SpeakerPictures pictures, string? picture, double size, string? unsetStatus = null, params Control[] beside)
    {
        ArgumentNullException.ThrowIfNull(pictures);

        _pictures = pictures;
        _picture = picture;
        _size = size;
        _unsetStatus = unsetStatus;
        _beside = beside;
        Spacing = 6;
        Build();
    }

    private void Build()
    {
        var pictures = _pictures;
        var picture = _picture;
        var holder = this;
        var beside = _beside;

        holder.Children.Clear();

        if (pictures.Find(picture) is { } file)
        {
            try
            {
                using var bytes = new MemoryStream(pictures.Files.ReadBytes(file) ?? throw new FileNotFoundException("The picture is gone.", file));
                holder.Children.Add(new Image
                {
                    Source = new Bitmap(bytes),
                    MaxWidth = _size,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                holder.Children.Add(AdventuresPage.Muted("The picture could not be read."));
            }
        }

        var status = AdventuresPage.Text(picture is null ? _unsetStatus ?? string.Empty : string.Empty, TypeScale.Small, ThemeManager.GreyKey);
        var change = new Button { Content = "Change picture", IsEnabled = picture is not null };
        var restore = new Button { Content = "Use the default", IsEnabled = picture is not null && pictures.IsChosen(picture) };

        change.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
            {
                status.Text = "No file picker here.";
                return;
            }

            var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose a picture",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Pictures") { Patterns = PictureImport.Patterns }],
            });

            if (picked.Count == 0)
            {
                return;
            }

            change.IsEnabled = false;
            string? refusal;

            try
            {
                await using var stream = await picked[0].OpenReadAsync();
                var name = picked[0].Name;
                refusal = await Task.Run(() => PictureImport.Save(pictures.Files, stream, name, pictures.Chosen(picture!)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                refusal = $"The picture could not be saved: {ex.Message}";
            }

            if (refusal is not null)
            {
                change.IsEnabled = true;
                status.Text = refusal;
                return;
            }

            Build();
        };

        restore.Click += (_, _) =>
        {
            try
            {
                pictures.UseDefault(picture!);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                status.Text = $"Your picture could not be removed: {ex.Message}";
                return;
            }

            Build();
        };

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            Children = { change, restore },
        };

        foreach (var control in beside)
        {
            (control.Parent as Avalonia.Controls.Panel)?.Children.Remove(control);
            buttons.Children.Add(control);
        }

        holder.Children.Add(buttons);
        holder.Children.Add(status);
    }
}
