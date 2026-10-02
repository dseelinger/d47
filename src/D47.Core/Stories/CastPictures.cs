using System.Text.RegularExpressions;

namespace D47.Core.Stories;

/// <summary>
/// A cast member's picture on disk: the Commander's own <c>&lt;picture&gt;.png</c> in <see cref="AppPaths.Pictures"/>
/// first, then the story's downloaded <c>&lt;picture&gt;.jpg</c> in <see cref="AppPaths.Stories"/>.
/// </summary>
public sealed partial class CastPictures(string chosenFolder, string storiesFolder)
{
    public CastPictures(AppPaths paths)
        : this(paths.Pictures, paths.Stories)
    {
    }

    /// <summary>Where the Commander's file for <paramref name="picture"/> is kept.</summary>
    public string Chosen(string picture) => Path.Combine(chosenFolder, picture + ".png");

    /// <summary>Where the story's own file for <paramref name="picture"/> is downloaded.</summary>
    public string Default(string picture) => Path.Combine(storiesFolder, picture + ".jpg");

    /// <summary>Whether <paramref name="picture"/> is a picture name and not a path.</summary>
    public static bool IsName(string? picture) =>
        picture is { Length: > 0 } && !picture.Contains("..", StringComparison.Ordinal) && Name().IsMatch(picture);

    /// <summary>Whether the Commander has chosen their own file for <paramref name="picture"/>.</summary>
    public bool IsChosen(string? picture) => IsName(picture) && File.Exists(Chosen(picture!));

    /// <summary>The file to show for <paramref name="picture"/>, or null when neither is on disk.</summary>
    public string? Find(string? picture)
    {
        if (!IsName(picture))
        {
            return null;
        }

        var chosen = Chosen(picture!);

        if (File.Exists(chosen))
        {
            return chosen;
        }

        var shipped = Default(picture!);
        return File.Exists(shipped) ? shipped : null;
    }

    /// <summary>
    /// The picture a line from <paramref name="speaker"/> carries: a primary member's always, another member's
    /// when its picture is on disk, and none for the ship, the narrator or a member without one.
    /// </summary>
    public string? For(StorySpeakerShown? speaker) =>
        speaker is { } shown && IsName(shown.Picture) && (shown.Primary || Find(shown.Picture) is not null)
            ? shown.Picture
            : null;

    /// <summary>Deletes the Commander's file, so the story's own shows again.</summary>
    public void UseDefault(string picture)
    {
        if (!IsName(picture))
        {
            return;
        }

        File.Delete(Chosen(picture));
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex Name();
}
