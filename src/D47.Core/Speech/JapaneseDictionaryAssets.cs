using D47.Core.Storage;

namespace D47.Core.Speech;

/// <summary>The IPAdic files MeCab needs to read Japanese text as kana, fetched from a d47 release.</summary>
public static class JapaneseDictionaryAssets
{
    public const string Release = "japanese-dictionary-1";

    public const string Repository = "dseelinger/d47";

    public const string Host = "github.com";

    private const string Source = "https://github.com/" + Repository + "/releases/download/" + Release + "/";

    /// <summary>The five files, smallest first.</summary>
    public static readonly IReadOnlyList<KokoroAsset> All =
    [
        Asset("dicrc", 725, "7111c8ca9db268989b2c79de022f5a055dce10b3e9c2ac8994ba41626ac793d3"),
        Asset("unk.dic", 5_690, "a5accef0d977c92f6ff5cc30a6428df2db8390b3d3a315273de9851314fa0db4"),
        Asset("char.bin", 262_496, "81bba502ae48fa005a374819f15e44452eb68086a8b323ec20a044d514400832"),
        Asset("matrix.bin", 3_463_716, "ee44d7350cdcb680ebd699f83e121be1dc63310f8832d55bcb537a068177611a"),
        Asset("sys.dic", 49_205_956, "c72dfc026322b6b8f6bbc8d90fd6706bef9d566d9b36b061eac8c494279fc427"),
    ];

    /// <summary>Where one file lands under <paramref name="folder"/>.</summary>
    public static string Destination(string folder, KokoroAsset asset) =>
        System.IO.Path.Combine(folder, asset.Path);

    /// <summary>Whether every file is present at its pinned size.</summary>
    public static bool IsInstalled(IFileSystem fileSystem, string folder) =>
        All.All(asset => fileSystem.Stat(Destination(folder, asset)) is { } file
                         && file.Length == asset.Bytes);

    public static double TotalMegabytes => All.Sum(asset => asset.Bytes) / 1024.0 / 1024.0;

    private static KokoroAsset Asset(string file, long bytes, string sha256) =>
        new(file, Repository, bytes, sha256) { Url = Source + file };
}
