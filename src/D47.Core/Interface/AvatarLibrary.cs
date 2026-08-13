using D47.Core.Audio;

namespace D47.Core.Interface;

/// <summary>
/// The Commander's own avatar imagery, per loop state (list.md Phase 11, "Ship's AI Avatar").
/// <para>
/// <b>The shipped set is not here.</b> It is drawn by the panel itself as vector shapes with
/// Avalonia's own animation, for one reason: one widget tree renders to both the desktop window
/// and the headset, and a decoded image frame that something has to advance would need driving
/// on both surfaces. Shapes and an animation are advanced by the framework on whichever surface
/// they are rendered to, and the theme repaints them for free. That also means "animated formats
/// supported" costs no image decoder in the dependency graph.
/// </para>
/// <para>
/// What <em>is</em> here is the override. A folder per state, any readable image in it joins
/// that state's sequence, and a folder with more than one file animates by cycling them — the
/// same shape the audio cues use, so a Commander who has already dropped in their own sounds
/// knows this without being told. Resolution is per state and all-or-nothing: replacing
/// <c>thinking</c> leaves the other seven alone.
/// </para>
/// <para>
/// <b>Readability is checked here; decoding is not.</b> A file is admitted on the evidence the
/// filesystem can give — a known extension, non-empty, openable. A file that passes that and is
/// still a truncated PNG reaches the panel, which falls back to the drawn frame rather than
/// showing a broken box. Both halves are needed: this one keeps an unreadable file from ever
/// being offered, that one keeps a corrupt file from breaking the surface.
/// </para>
/// </summary>
public sealed class AvatarLibrary
{
    /// <summary>
    /// What the panel will attempt. No SVG and no GIF: Avalonia decodes neither without a
    /// package, and offering an extension that silently never renders is worse than not
    /// offering it. A sequence of PNGs in one folder is how an animation is supplied.
    /// </summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".bmp", ".webp"];

    private readonly Dictionary<LoopState, IReadOnlyList<string>> _frames = [];

    /// <summary>Where the Commander drops their own, beside the executable like everything else.</summary>
    public static string FolderFor(AppPaths paths, LoopState state) =>
        Path.Combine(paths.Data, "avatar", state.ToString().ToLowerInvariant());

    /// <summary>
    /// Scans every state's folder. Called at startup and again when the Commander asks for a
    /// reload; cheap enough that there is no reason to cache across calls.
    /// </summary>
    public static AvatarLibrary Load(AppPaths paths)
    {
        var library = new AvatarLibrary();

        foreach (var state in Enum.GetValues<LoopState>())
        {
            if (Scan(FolderFor(paths, state)) is { Count: > 0 } frames)
            {
                library._frames[state] = frames;
            }
        }

        return library;
    }

    /// <summary>
    /// The Commander's frames for this state, or empty when they have supplied none — in which
    /// case the panel draws its own. Empty is the overwhelmingly common answer.
    /// </summary>
    public IReadOnlyList<string> For(LoopState state) =>
        _frames.TryGetValue(state, out var frames) ? frames : [];

    /// <summary>Whether anything has been dropped in at all. For diagnostics and the docs.</summary>
    public bool Any => _frames.Count > 0;

    /// <summary>Which states the Commander has replaced. Reported rather than inferred.</summary>
    public IReadOnlyList<LoopState> Replaced => [.. _frames.Keys.Order()];

    private static IReadOnlyList<string> Scan(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                return [];
            }

            return
            [
                // Ordered by name, so a Commander numbering their frames gets them in that
                // order and a reload does not reshuffle the animation.
                .. Directory
                    .EnumerateFiles(folder)
                    .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    .Where(Readable)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be read is a folder with nothing in it, as far as the panel
            // is concerned. The drawn frame is always there to fall back to.
            return [];
        }
    }

    /// <summary>
    /// A non-empty file that opens. Enough to keep a zero-byte or locked file out of the
    /// sequence rather than serving it and getting a blank frame.
    /// </summary>
    private static bool Readable(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            return stream.Length > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
