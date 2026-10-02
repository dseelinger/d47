using D47.Core.Audio;

namespace D47.Core.Interface;

/// <summary>Where the avatar clip for a core and loop state lives on disk.</summary>
public static class CoreClips
{
    private const string OwnPrefix = "own.";
    private const string OwnDirectory = "custom";

    /// <summary><c>&lt;dir&gt;.&lt;state&gt;.mp4</c>, where dir is <c>custom</c> for an own core and the core id otherwise.</summary>
    public static string FileName(string coreId, LoopState state)
    {
        var directory = coreId.StartsWith(OwnPrefix, StringComparison.Ordinal) ? OwnDirectory : coreId;

        return $"{directory}.{state.ToString().ToLowerInvariant()}.mp4";
    }

    /// <summary>The clip's path when the file is non-empty and opens, otherwise null.</summary>
    public static string? For(string folder, string coreId, LoopState state)
    {
        var path = Path.Combine(folder, FileName(coreId, state));

        return AvatarLibrary.Readable(path) ? path : null;
    }
}
