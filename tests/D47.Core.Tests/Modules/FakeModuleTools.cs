using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Modules;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Tests.Modules;

/// <summary>Modules held in memory: the tools offered, the answer each call gets, and the calls made.</summary>
public sealed class FakeModuleTools : IModuleTools
{
    private IReadOnlyList<ModuleToolOffer> _tools = [];

    public IReadOnlyList<ModuleToolOffer> Tools => _tools;

    /// <summary>Installed module names by id.</summary>
    public Dictionary<string, string> Installed { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Running { get; } = new(StringComparer.Ordinal);

    public ModuleCallResult Answer { get; set; } = new(false, "Three bounties open.");

    public List<(string ModuleId, string Tool, IReadOnlyDictionary<string, object?> Arguments)> Calls { get; } = [];

    /// <summary>Installs and runs a module offering these tools, replacing what it offered before.</summary>
    public FakeModuleTools Offering(string moduleId, string name, params ModuleToolOffer[] tools)
    {
        Installed[moduleId] = name;
        Running.Add(moduleId);
        _tools = [.. _tools.Where(tool => tool.ModuleId != moduleId), .. tools];
        return this;
    }

    public void Stop(string moduleId)
    {
        Running.Remove(moduleId);
        _tools = [.. _tools.Where(tool => tool.ModuleId != moduleId)];
    }

    public string? InstalledName(string moduleId) => Installed.GetValueOrDefault(moduleId);

    public bool IsRunning(string moduleId) => Running.Contains(moduleId);

    public Task<ModuleCallResult> CallAsync(
        string moduleId, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellation)
    {
        Calls.Add((moduleId, tool, arguments));
        return Task.FromResult(Answer);
    }

    public static ModuleToolOffer Tool(
        string moduleId,
        string tool,
        string? description = "Opens the bounty board.",
        string schema = """{"type":"object","properties":{}}""",
        params string[] phrases) =>
        new(moduleId, moduleId == "bounty" ? "Bounty Board" : moduleId, tool, description, schema, phrases);

    /// <summary>Attaches these modules to the registry and returns the router the App builds over them.</summary>
    public KeywordRouter AttachTo(CapabilityRegistry registry, Func<IReadOnlyList<DynamicCommand>>? others = null)
    {
        var modules = new ModuleTools(this, registry, NullLogger.Instance);
        registry.Modules = modules;

        return new KeywordRouter(registry, () =>
        {
            var own = others?.Invoke() ?? [];
            return [.. own, .. modules.Phrases(own)];
        });
    }
}
