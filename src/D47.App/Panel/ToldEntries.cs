using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Adventures;

namespace D47.App.Panel;

/// <summary>A chapter's told entries as the chapter's page and the mini panel draw them: stamp line, speaker, text and portrait.</summary>
internal sealed class ToldEntries(AdventureSurface surface, Func<SpeakerPortraits?> portraits)
{
    /// <summary>One told entry. Without <paramref name="copy"/> the trigger has no copy glyph.</summary>
    public Control Entry(Adventure adventure, AdventureTold told, string? here, Func<string, Task<bool>>? copy)
    {
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };

        if (told.Kind == AdventureToldKind.Beat && told.Line > 0)
        {
            stack.Margin = new Thickness(0, 4, 0, 0);
        }
        else if (told.Kind == AdventureToldKind.Beat)
        {
            var heading = told.Title is { Length: > 0 } title ? title : adventure.Name;

            stack.Children.Add(Stamped($"{heading} — ", told.At));

            if (told.Trigger is { Length: > 0 } trigger)
            {
                var system = told.Beat >= 0 && told.Beat < adventure.Beats.Count ? adventure.Beats[told.Beat].Steps[^1].Trigger.System : null;
                stack.Children.Add(AdventuresPage.Trigger(AdventuresPage.Sentence(trigger), here, system, copy));
            }
        }
        else if (told.Kind == AdventureToldKind.Nudge)
        {
            stack.Children.Add(Stamped("Narrator — ", told.At));
        }
        else
        {
            // The Commander's own words above the answer, so an aside reads as an exchange rather than as
            // a paragraph of story that arrived from nowhere.
            stack.Children.Add(
                told.Asked is { Length: > 0 } asked
                    ? Stamped("You asked, ", told.At, $" — “{asked}”")
                    : Stamped("Aside — ", told.At));
        }

        var column = new StackPanel { Spacing = 2 };

        if (told.Kind == AdventureToldKind.Beat && told.Speaker is { Length: > 0 } speaker)
        {
            column.Children.Add(AdventuresPage.Text(speaker, TypeScale.Small, ThemeManager.GreyKey));
        }

        column.Children.Add(AdventuresPage.Text(told.Text, TypeScale.Body));

        stack.Children.Add(Pictured(
            column,
            told.Kind switch
            {
                AdventureToldKind.Beat => told.Picture,
                AdventureToldKind.Nudge => D47.Core.Interface.SpeakerPictures.Narrator,
                _ => surface.ShipPicture?.Invoke(),
            }));

        return stack;
    }

    /// <summary>The name a story speaker id is shown under: the cast member's, "Narrator", or null for the ship.</summary>
    public string? SpeakerName(string? storyId, string? speaker) => speaker switch
    {
        null or D47.Core.Stories.StorySpeaker.Ship => null,
        D47.Core.Stories.StorySpeaker.Narrator => D47.Core.Audio.VoiceRoles.Called(D47.Core.Audio.VoiceRole.Narrator),
        _ => (storyId is null ? null : surface.Stories?.Speaker(surface.Commander(), storyId, speaker))?.Name ?? speaker,
    };

    /// <summary>The picture a story speaker id is shown with: the Narrator's, the core's for the ship, or the cast member's.</summary>
    public string? PictureOf(string? storyId, string? speaker) => speaker switch
    {
        null or D47.Core.Stories.StorySpeaker.Ship => surface.ShipPicture?.Invoke(),
        D47.Core.Stories.StorySpeaker.Narrator => D47.Core.Interface.SpeakerPictures.Narrator,
        _ => storyId is null ? null : surface.Pictures?.For(surface.Stories?.Speaker(surface.Commander(), storyId, speaker)),
    };

    /// <summary><paramref name="line"/> with its speaker's picture to the left, top-aligned; the line alone when the picture has no file.</summary>
    public Control Pictured(Control line, string? picture)
    {
        if (portraits() is not { } decoded || decoded.For(picture) is not { } bitmap)
        {
            return line;
        }

        var image = new Image
        {
            Name = "SpeakerPicture",
            Source = bitmap,
            Width = SpeakerPortraits.Size,
            Height = SpeakerPortraits.Size,
            Stretch = Stretch.UniformToFill,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true,
            Margin = new Thickness(0, 0, PanelView.PictureGap, 0),
        };

        SpeakerPictureViewer.Enlarges(image, decoded.Pictures, picture);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(line, 1);
        row.Children.Add(image);
        row.Children.Add(line);
        return row;
    }

    /// <summary>A grey caption with its local time in mono between <paramref name="before"/> and <paramref name="after"/>.</summary>
    public static TextBlock Stamped(string before, DateTimeOffset at, string after = "")
    {
        var block = new TextBlock { FontSize = TypeScale.Small, TextWrapping = TextWrapping.Wrap };
        block.Inlines = [new Run(before), Fonts.Mono(at.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.CurrentCulture))];

        if (after.Length > 0)
        {
            block.Inlines.Add(new Run(after));
        }

        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }
}
