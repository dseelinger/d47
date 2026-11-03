namespace D47.Core.Modules;

/// <summary>A tool a running module offers. Every text field is the module's own, untrusted.</summary>
/// <param name="ModuleName">The manifest's name.</param>
/// <param name="InputSchemaJson">The tool's JSON Schema as the module sent it.</param>
/// <param name="Phrases">The exact phrases the module gave in <c>_meta</c> as <c>"d47/phrases"</c>.</param>
public sealed record ModuleToolOffer(
    string ModuleId,
    string ModuleName,
    string Tool,
    string? Description,
    string InputSchemaJson,
    IReadOnlyList<string> Phrases);

/// <summary>What a module's tool returned: its text, untrusted.</summary>
public sealed record ModuleCallResult(bool IsError, string Text);

/// <summary>The running modules' tools, which the capability registry falls through to for a name it does not own.</summary>
public interface IModuleTools
{
    /// <summary>
    /// The tools of every module running now. Returns at once, since the tick and turn assembly read it; the same
    /// instance until a module's tools change.
    /// </summary>
    IReadOnlyList<ModuleToolOffer> Tools { get; }

    /// <summary>The installed module's name, or null when no module with that id is installed.</summary>
    string? InstalledName(string moduleId);

    bool IsRunning(string moduleId);

    Task<ModuleCallResult> CallAsync(
        string moduleId, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellation);
}
