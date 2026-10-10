using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Audio;
using D47.Core.Storage;
using D47.Core.Stories;

namespace D47.Core.Interface;

/// <summary>
/// A speaker's picture on disk: the Commander's own <c>&lt;picture&gt;.png</c> in <see cref="AppPaths.Pictures"/>
/// first, then the shipped <c>&lt;picture&gt;.jpg</c> in <see cref="AppPaths.ShippedPortraits"/>, then a story's
/// downloaded <c>&lt;picture&gt;.jpg</c> in <see cref="AppPaths.Stories"/>.
/// </summary>
public sealed partial class SpeakerPictures(IFileSystem files, string chosenFolder, string shippedFolder, string storiesFolder)
{
    /// <summary>The Narrator's picture name.</summary>
    public const string Narrator = "narrator";

    public SpeakerPictures(IFileSystem files, AppPaths paths)
        : this(files, paths.Pictures, paths.ShippedPortraits, paths.Stories)
    {
    }

    /// <summary>A core's picture name, from its persona id.</summary>
    public static string Core(string personaId) => "core." + personaId;

    /// <summary>A Commander's picture name, from their Frontier id.</summary>
    public static string Commander(string frontierId) => "commander." + frontierId;

    /// <summary>A hired pilot's picture name, from their crew id.</summary>
    public static string Crew(long crewId) => "crew." + crewId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The picture name of the carrier captain or the tower speaking in a voice of <paramref name="gender"/>; null for
    /// any other role, or a voice whose listing gives no gender.
    /// </summary>
    public static string? ByVoice(VoiceRole role, VoiceGender gender)
    {
        var prefix = role switch
        {
            VoiceRole.CarrierCaptain => "captain",
            VoiceRole.TowerControl => "tower",
            _ => null,
        };

        return (prefix, gender) switch
        {
            (null, _) => null,
            (_, VoiceGender.Masculine) => prefix + ".man",
            (_, VoiceGender.Feminine) => prefix + ".woman",
            _ => null,
        };
    }

    /// <summary>Where the Commander's file for <paramref name="picture"/> is kept.</summary>
    public string Chosen(string picture) => Path.Combine(chosenFolder, picture + ".png");

    /// <summary>Where the build's own file for <paramref name="picture"/> is installed.</summary>
    public string Shipped(string picture) => Path.Combine(shippedFolder, picture + ".jpg");

    /// <summary>Where the story's own file for <paramref name="picture"/> is downloaded.</summary>
    public string Default(string picture) => Path.Combine(storiesFolder, picture + ".jpg");

    /// <summary>Whether <paramref name="picture"/> is a picture name and not a path.</summary>
    public static bool IsName(string? picture) =>
        picture is { Length: > 0 } && !picture.Contains("..", StringComparison.Ordinal) && Name().IsMatch(picture);

    /// <summary>Whether the Commander has chosen their own file for <paramref name="picture"/>.</summary>
    public bool IsChosen(string? picture) => IsName(picture) && files.Stat(Chosen(picture!)) is not null;

    /// <summary>The file to show for <paramref name="picture"/>, or null when none is on disk.</summary>
    public string? Find(string? picture)
    {
        if (!IsName(picture))
        {
            return null;
        }

        foreach (var file in (string[])[Chosen(picture!), Shipped(picture!), Default(picture!)])
        {
            if (files.Stat(file) is not null)
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>
    /// The picture a line from <paramref name="speaker"/> carries: a primary member's always, another member's
    /// when its picture is on disk, and none for the ship, the narrator or a member without one.
    /// </summary>
    public string? For(StorySpeakerShown? speaker) =>
        speaker is { } shown && IsName(shown.Picture) && (shown.Primary || Find(shown.Picture) is not null)
            ? shown.Picture
            : null;

    /// <summary>Deletes the Commander's file, so the shipped or story's own shows again.</summary>
    public void UseDefault(string picture)
    {
        if (!IsName(picture))
        {
            return;
        }

        files.Delete(Chosen(picture));
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex Name();
}
