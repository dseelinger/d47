using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Capabilities;
using Xunit;

namespace D47.App.Tests;

/// <summary>Invokes a tool as the model would and opens the page it returns.</summary>
public static class AskedByVoice
{
    public static async Task<ToolResult> AskAsync(
        CapabilityRegistry registry,
        PanelRouting routing,
        string tool,
        params (string Name, string Value)[] values)
    {
        var result = await registry.InvokeAsync(
            tool,
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        if (result.Page is { } page)
        {
            routing.Open(page);
        }

        Dispatcher.UIThread.RunJobs();

        return result;
    }
}
