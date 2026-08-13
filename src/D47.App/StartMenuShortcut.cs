using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace D47.App;

/// <summary>
/// A Start Menu entry for a portable executable, offered once and never assumed.
/// <para>
/// d47 does not install (list.md Phase 1, architecture.md §9): one self-contained file, per
/// user, no elevation, no MSI, and everything it writes beside the executable. The cost of that
/// is a program the Commander can only reach by remembering where they put it. A shortcut is
/// the cheapest fix that does not turn this into an installer — one file, in the folder Windows
/// keeps for exactly this, created only when asked for and removable by hand.
/// </para>
/// <para>
/// <b>On "everything goes to data/, never %APPDATA%".</b> That invariant is about d47's own
/// state — settings, secrets, logs, view state — so that copying the folder copies the program.
/// A Start Menu shortcut is not d47's state: it is a Windows artefact, in a Windows-defined
/// location, that exists to point at d47 rather than to describe it. Deleting it loses nothing,
/// and it is only ever created on an explicit yes.
/// </para>
/// <para>
/// It points at the executable's path rather than at a copy, so the in-place update — which
/// replaces the file and keeps the path — leaves the shortcut still pointing at the current
/// build, exactly as a pinned taskbar icon does.
/// </para>
/// </summary>
public static class StartMenuShortcut
{
    /// <summary>What the entry is called, and therefore what the Commander types to find it.</summary>
    public const string EntryName = "Directive 47";

    /// <summary>
    /// The per-user Start Menu, never the machine-wide one: the all-users folder needs
    /// elevation, and needing elevation is the thing this is designed around.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        $"{EntryName}.lnk");

    /// <summary>Whether an entry is already there, in which case there is nothing to offer.</summary>
    public static bool Exists() => File.Exists(DefaultPath);

    /// <summary>
    /// Writes the shortcut. Returns false with a reason rather than throwing: failing to create
    /// a convenience is not a failure of the program, and the Commander can always run the
    /// executable exactly as they just did.
    /// </summary>
    public static bool TryCreate(string shortcutPath, string target, ILogger logger)
    {
        try
        {
            var directory = Path.GetDirectoryName(shortcutPath);

            if (directory is { Length: > 0 })
            {
                Directory.CreateDirectory(directory);
            }

            var link = (IShellLinkW)new ShellLink();

            link.SetPath(target);
            link.SetDescription("Elite Dangerous voice companion");

            // The folder the executable is in, so the data folder beside it resolves the same
            // way it does when the Commander runs it directly.
            link.SetWorkingDirectory(Path.GetDirectoryName(target) ?? string.Empty);

            // Index 0 of the executable itself: the icon is compiled into it, so this keeps
            // working across an update that replaces the file.
            link.SetIconLocation(target, 0);

            ((IPersistFile)link).Save(shortcutPath, fRemember: true);

            logger.LogInformation("Added a Start Menu entry at {Path}", shortcutPath);

            return true;
        }
        // ArgumentException included deliberately: a path the framework rejects before any I/O
        // happens - an embedded null, a reserved character - arrives here as an argument fault,
        // and it is still just "no shortcut" rather than something to take the app down over.
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException
                                       or NotSupportedException or InvalidCastException
                                       or ArgumentException)
        {
            logger.LogWarning(ex, "Could not add a Start Menu entry at {Path}", shortcutPath);

            return false;
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink;

    /// <summary>
    /// The shell's shortcut interface. Every member is declared, in vtable order, because COM
    /// dispatches by slot — omitting an unused one silently calls the wrong function.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath(
            [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder file,
            int maxPath,
            nint find,
            int flags);

        void GetIDList(out nint idList);

        void SetIDList(nint idList);

        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder name, int maxName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder directory, int maxPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder arguments, int maxArguments);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);

        void GetHotkey(out short hotkey);

        void SetHotkey(short hotkey);

        void GetShowCmd(out int show);

        void SetShowCmd(int show);

        void GetIconLocation(
            [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder iconPath,
            int iconPathLength,
            out int icon);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int icon);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relative, int reserved);

        void Resolve(nint window, int flags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);

        [PreserveSig]
        int IsDirty();

        void Load([MarshalAs(UnmanagedType.LPWStr)] string file, int mode);

        void Save([MarshalAs(UnmanagedType.LPWStr)] string? file, [MarshalAs(UnmanagedType.Bool)] bool fRemember);

        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string file);

        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string file);
    }
}
