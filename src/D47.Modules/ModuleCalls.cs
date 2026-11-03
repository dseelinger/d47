using System.Text.Json;

namespace D47.Modules;

/// <summary>A tool a module offers. Every field is the module's own, untrusted text.</summary>
/// <param name="Phrases">The strings in the tool's <c>_meta</c> under <c>"d47/phrases"</c>.</param>
public sealed record ModuleTool(string ModuleId, string Name, string? Description, JsonElement InputSchema, IReadOnlyList<string> Phrases);

/// <summary>What a tool call returned: its text parts joined, untrusted.</summary>
public sealed record ModuleToolResult(bool IsError, string Text);

/// <summary>The text parts of a resource, untrusted; binary parts are left out.</summary>
public sealed record ModuleResource(string Uri, string? MimeType, string Text);
