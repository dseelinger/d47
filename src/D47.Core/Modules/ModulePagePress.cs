using System.Text;
using System.Text.Json;
using D47.Core.Capabilities;

namespace D47.Core.Modules;

/// <summary>Runs a press on a module page: always the page's own module's tool, always as the Commander.</summary>
public static class ModulePagePress
{
    /// <summary>The registry's name for <paramref name="tool"/> of module <paramref name="moduleId"/>.</summary>
    public static string ToolName(string moduleId, string tool) => $"{ModuleTools.Prefix}{moduleId}_{tool}";

    /// <summary>A TileButton's call.</summary>
    public static Task<ToolResult> CallAsync(
        Func<string, ToolArguments, ToolCaller, CancellationToken, Task<ToolResult>> invoke,
        string moduleId,
        ModuleToolCall call,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        ArgumentNullException.ThrowIfNull(call);

        return invoke(ToolName(moduleId, call.Tool), ToolArguments.FromJson(call.ArgumentsJson), ToolCaller.Commander, cancellation);
    }

    /// <summary>A CheckboxTile, Segmented or NumberStepper sending <c>{argument: value}</c>.</summary>
    public static Task<ToolResult> SendAsync(
        Func<string, ToolArguments, ToolCaller, CancellationToken, Task<ToolResult>> invoke,
        string moduleId,
        ModuleSends sends,
        string valueJson,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        ArgumentNullException.ThrowIfNull(sends);

        return invoke(ToolName(moduleId, sends.Tool), ToolArguments.FromJson(Object(sends.Argument, valueJson)), ToolCaller.Commander, cancellation);
    }

    private static string Object(string argument, string valueJson)
    {
        using var value = JsonDocument.Parse(valueJson);
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName(argument);
            value.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
