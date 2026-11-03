using System.Globalization;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// A module for the integration tests: echo, pid, say and sample tools over stdio.
// "--protocol <version>" makes it offer that protocol version instead of the newest; "--linger" keeps it
// running for a second and a half after its input closes.
var options = new McpServerOptions
{
    ProtocolVersion = args is ["--protocol", var version, ..] ? version : null,
    ServerInfo = new Implementation { Name = "fake", Version = "1.0.0" },
    ToolCollection =
    [
        McpServerTool.Create((string text) => text, new McpServerToolCreateOptions { Name = "echo" }),
        McpServerTool.Create(
            () => Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            new McpServerToolCreateOptions { Name = "pid" }),
        McpServerTool.Create(
            async (McpServer server, string key, string text, CancellationToken cancellation) =>
            {
                await server.SendNotificationAsync(
                    "notifications/d47/say",
                    new JsonObject { ["key"] = key, ["text"] = text },
                    cancellationToken: cancellation);
                return "sent";
            },
            new McpServerToolCreateOptions { Name = "say" }),
        McpServerTool.Create(
            async (McpServer server, CancellationToken cancellation) =>
            {
                try
                {
                    await server.SendRequestAsync(
                        new JsonRpcRequest
                        {
                            Method = "sampling/createMessage",
                            Params = JsonNode.Parse("""{"messages":[{"role":"user","content":{"type":"text","text":"hello"}}],"maxTokens":10}"""),
                        },
                        cancellation);
                    return "answered";
                }
                catch (Exception ex)
                {
                    return $"refused: {ex.GetType().Name}: {ex.Message}";
                }
            },
            new McpServerToolCreateOptions { Name = "sample" }),
    ],
};

await using var server = McpServer.Create(new StdioServerTransport("fake"), options);
await server.RunAsync();

if (args.Contains("--linger"))
{
    await Task.Delay(1500);
}
