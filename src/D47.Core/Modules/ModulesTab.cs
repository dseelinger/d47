using System.Globalization;

namespace D47.Core.Modules;

/// <summary>What a module's page shows in place of the page itself.</summary>
public enum ModulePageState
{
    Starting,
    NotResponding,
    Stopped,
    Off,
    NotArrived,
}

/// <summary>The words the Modules tab draws: the installed list, and the states a module's page can be in.</summary>
public static class ModulesTab
{
    public const string Summary = "Every module installed, and the pages they draw.";

    public const string ListFooter = "D47 runs only the modules you turn on. Turn them on or off, and pick their voices, in Settings.";

    public const string NotArrivedLabel = "This page did not arrive";

    public const string NotRespondingText = "It has stopped answering D47. This page shows what it sent last. Restart it, or wait for it to answer.";

    public const string StoppedText =
        "It crashed each time D47 restarted it, so D47 stopped restarting it. Restart it to try again, or turn it off in Settings › Privacy and this install › Modules.";

    public static string PageSummary(string author) => By(author) + ".";

    private static string By(string author) => $"A module by {author}";

    public static string StartingText(string name) => $"{name} is starting. Its page appears when it answers.";

    public static string NotRespondingLabel(string name) => $"{name} is not responding";

    public static string StoppedLabel(string name) => $"{name} stopped";

    public static string OffText(string name) => $"{name} is off. Its tools, lines and pages come back when you turn it on.";

    public static string NotArrivedText(string name) => $"{name} is running, but it did not send this page. Try again, or restart it.";

    /// <summary>The line under a notice saying when the module last answered, in the Commander's local time.</summary>
    public static string LastAnswer(DateTimeOffset? at) =>
        at is { } answered
            ? string.Create(CultureInfo.InvariantCulture, $"Last answer {answered.ToLocalTime():HH:mm}")
            : "No answer yet";

    /// <summary>The status as the installed list's aside gives it.</summary>
    public static string StatusWord(ModuleStatus status) => status switch
    {
        ModuleStatus.Running => "Running",
        ModuleStatus.Starting => "Starting",
        ModuleStatus.NotResponding => "Not responding",
        ModuleStatus.StoppedAfterRepeatedCrashes => "Stopped after repeated crashes",
        ModuleStatus.Refused => "Refused",
        _ => "Off",
    };

    /// <summary>A module with a page can be opened; one with none, and a refused one, cannot.</summary>
    public static bool Openable(InstalledModule module) => module is ReadyModule { Manifest.Pages.Count: > 0 };

    /// <summary>What a module is called: its manifest name, or its folder's name when it was refused.</summary>
    public static string NameOf(InstalledModule module) => module switch
    {
        ReadyModule ready => ready.Manifest.Name,
        _ => Path.GetFileName(module.Folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
    };

    /// <summary>The line under a module's name in the installed list.</summary>
    public static string RowSub(InstalledModule module) => module switch
    {
        ReadyModule { Manifest: var manifest } => manifest.Pages.Count switch
        {
            0 => $"{By(manifest.Author)} · tools and lines, no page",
            1 => $"{By(manifest.Author)} · 1 page",
            var count => string.Create(CultureInfo.InvariantCulture, $"{By(manifest.Author)} · {count} pages"),
        },
        RefusedModule { Reason: ModuleRefusalReason.UnknownContract } => "Built for a contract this D47 does not know",
        _ => "D47 will not start it",
    };

    /// <summary>The module's status, Off when none has been reported.</summary>
    public static ModuleStatus StatusOf(InstalledModule module, IReadOnlyList<ModuleStatusReport> statuses) =>
        module is RefusedModule
            ? ModuleStatus.Refused
            : statuses.FirstOrDefault(s => string.Equals(s.Folder, module.Folder, StringComparison.OrdinalIgnoreCase))?.Status ?? ModuleStatus.Off;

    public static ModuleStatusReport? ReportOf(InstalledModule module, IReadOnlyList<ModuleStatusReport> statuses) =>
        statuses.FirstOrDefault(s => string.Equals(s.Folder, module.Folder, StringComparison.OrdinalIgnoreCase));

    /// <summary>What a module's page shows for its status. Until pages arrive, a running module's page has not.</summary>
    public static ModulePageState StateOf(ModuleStatus status) => status switch
    {
        ModuleStatus.Starting => ModulePageState.Starting,
        ModuleStatus.NotResponding => ModulePageState.NotResponding,
        ModuleStatus.StoppedAfterRepeatedCrashes => ModulePageState.Stopped,
        ModuleStatus.Running => ModulePageState.NotArrived,
        _ => ModulePageState.Off,
    };

    public static string PageKey(string moduleId) => "module:" + moduleId;

    /// <summary>The module id a level key names, or null for a key that is not a module's.</summary>
    public static string? IdOfKey(string key) => key.StartsWith("module:", StringComparison.Ordinal) ? key["module:".Length..] : null;
}
