using D47.Core.Modules;

namespace D47.App;

/// <summary>The running modules' tools once the host has started them; none before.</summary>
internal sealed class LateModuleTools(Func<IModuleTools?> modules) : IModuleTools
{
    public IReadOnlyList<ModuleToolOffer> Tools => modules()?.Tools ?? [];

    public string? InstalledName(string moduleId) => modules()?.InstalledName(moduleId);

    public bool IsRunning(string moduleId) => modules()?.IsRunning(moduleId) == true;

    public Task<ModuleCallResult> CallAsync(
        string moduleId, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellation) =>
        modules() is { } running
            ? running.CallAsync(moduleId, tool, arguments, cancellation)
            : Task.FromResult(new ModuleCallResult(true, $"The module {moduleId} is not running."));
}
