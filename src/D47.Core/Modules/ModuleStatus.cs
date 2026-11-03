namespace D47.Core.Modules;

public enum ModuleStatus
{
    Starting,
    Running,
    NotResponding,
    StoppedAfterRepeatedCrashes,
    Off,
    Refused,
}

/// <summary>One installed module's status.</summary>
/// <param name="Id">The manifest id, or null for a module refused before its id was read.</param>
/// <param name="LastAnswer">When the module last answered d47, or null before it first has.</param>
public sealed record ModuleStatusReport(string Folder, string? Id, ModuleStatus Status, DateTimeOffset? LastAnswer = null);
