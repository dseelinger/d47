namespace D47.Core.Viewing;

/// <summary>The video catalogues d47 searches, by the id <see cref="Configuration.WatchSettings.Sources"/> stores.</summary>
public static class VideoSources
{
    public const string InternetArchive = "archive";

    public const string Nasa = "nasa";

    public const string Blender = "blender";

    public const string ArchiveHost = "archive.org";

    /// <summary>The mirrors archive.org redirects a download to; which one varies per item.</summary>
    public const string ArchiveMirrors = "*.us.archive.org";

    public const string NasaSearchHost = "images-api.nasa.gov";

    public const string NasaAssetsHost = "images-assets.nasa.gov";

    public const string BlenderHost = "download.blender.org";

    /// <summary>Every source, in the order the catalogue lists them.</summary>
    public static IReadOnlyList<string> All { get; } = [InternetArchive, Nasa, Blender];

    /// <summary>The source's name as the catalogue shows it.</summary>
    public static string NameOf(string id) => id switch
    {
        InternetArchive => "Internet Archive",
        Nasa => "NASA Image and Video Library",
        Blender => "Blender Studio",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Not a video source."),
    };
}
