using D47.Core.Audio;
using D47.Core.Storage;

namespace D47.Core.Interface;

/// <summary>The Commander's own avatar imagery, per loop state (Phase 11, "Ship's AI Avatar").</summary>
public sealed class AvatarLibrary
{
    private readonly IFileSystem _fileSystem;

    /// <summary>What the panel will attempt.</summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".bmp", ".webp"];

    private readonly Dictionary<LoopState, IReadOnlyList<string>> _frames = [];

    private AvatarLibrary(IFileSystem fileSystem) => _fileSystem = fileSystem;

    /// <summary>The file system the frames are read through.</summary>
    public IFileSystem Files => _fileSystem;

    /// <summary>Where the core clips are read from, or null for none.</summary>
    public string? ClipFolder { get; init; }

    /// <summary>Where the Commander drops their own, beside the executable like everything else.</summary>
    public static string FolderFor(AppPaths paths, LoopState state) =>
        Path.Combine(paths.Data, "avatar", state.ToString().ToLowerInvariant());

    /// <summary>Scans every state's folder.</summary>
    public static AvatarLibrary Load(IFileSystem fileSystem, AppPaths paths)
    {
        var library = new AvatarLibrary(fileSystem) { ClipFolder = paths.AvatarClips };

        foreach (var state in Enum.GetValues<LoopState>())
        {
            if (Scan(fileSystem, FolderFor(paths, state)) is { Count: > 0 } frames)
            {
                library._frames[state] = frames;
            }
        }

        return library;
    }

    /// <summary>
    /// The Commander's frames for this state, or empty when they have supplied none — in which case the
    /// panel draws its own.
    /// </summary>
    public IReadOnlyList<string> For(LoopState state) =>
        _frames.TryGetValue(state, out var frames) ? frames : [];

    /// <summary>The core's clip for a state from <see cref="ClipFolder"/>, or null when there is none.</summary>
    public string? ClipFor(string coreId, LoopState state) =>
        ClipFolder is { } folder ? CoreClips.For(_fileSystem, folder, coreId, state) : null;

    /// <summary>Whether anything has been dropped in at all.</summary>
    public bool Any => _frames.Count > 0;

    /// <summary>Which states the Commander has replaced.</summary>
    public IReadOnlyList<LoopState> Replaced => [.. _frames.Keys.Order()];

    private static IReadOnlyList<string> Scan(IFileSystem fileSystem, string folder)
    {
        try
        {
            return
            [
                // Ordered by name, so a Commander numbering their frames gets them in that order and a reload
                // does not reshuffle the animation.
                .. fileSystem
                    .Enumerate(folder, "*")
                    .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    .Where(file => Readable(fileSystem, file))
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be read is a folder with nothing in it, as far as the panel is concerned.
            return [];
        }
    }

    /// <summary>A non-empty file that opens.</summary>
    internal static bool Readable(IFileSystem fileSystem, string file)
    {
        try
        {
            using var stream = fileSystem.OpenRead(file);
            return stream is { Length: > 0 };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
