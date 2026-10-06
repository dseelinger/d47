using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>A cast member's picture, the Commander's own where they chose one, with the buttons that replace it.</summary>
public static class CastPicturePanel
{
    /// <summary>
    /// Fills <paramref name="holder"/> with the picture and its Change picture and Use the default buttons, then
    /// <paramref name="beside"/> in the same row.
    /// </summary>
    public static void Show(Control owner, StackPanel holder, SpeakerPictures pictures, string picture, params Control[] beside)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(pictures);

        holder.Children.Clear();

        if (pictures.Find(picture) is { } file)
        {
            try
            {
                using var bytes = new MemoryStream(File.ReadAllBytes(file));
                holder.Children.Add(new Image
                {
                    Source = new Bitmap(bytes),
                    MaxWidth = 240,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                holder.Children.Add(AdventuresPage.Muted("The picture could not be read."));
            }
        }

        var status = AdventuresPage.Text(string.Empty, TypeScale.Small, ThemeManager.GreyKey);
        var change = new Button { Content = "Change picture" };
        var restore = new Button { Content = "Use the default", IsEnabled = pictures.IsChosen(picture) };

        change.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(owner)?.StorageProvider is not { CanOpen: true } storage)
            {
                status.Text = "No file picker here.";
                return;
            }

            var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose a picture",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Pictures") { Patterns = CastPictureImport.Patterns }],
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
                refusal = await Task.Run(() => CastPictureImport.Save(stream, name, pictures.Chosen(picture)));
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

            Show(owner, holder, pictures, picture, beside);
        };

        restore.Click += (_, _) =>
        {
            try
            {
                pictures.UseDefault(picture);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                status.Text = $"Your picture could not be removed: {ex.Message}";
                return;
            }

            Show(owner, holder, pictures, picture, beside);
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
