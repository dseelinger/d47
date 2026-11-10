using D47.Core.Capabilities;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class AQueryFindsTheSettingTheModelNeedsTests
{
    private static async Task<string> ListAsync(string? query)
    {
        var surface = TestSurface.For(new MemoryInstall());
        var arguments = query is null
            ? ToolArguments.Empty
            : ToolArguments.FromJson(System.Text.Json.JsonSerializer.Serialize(new { query }));

        var listed = await surface.Registry.Invoke("list_settings", arguments);

        Assert.False(listed.IsError);
        return listed.Content;
    }

    private static string[] Keys(string listing) =>
        listing.Split('\n')
            .Where(line => !line.StartsWith(' ') && line.Contains(" — "))
            .Select(line => line[..line.IndexOf(" — ", StringComparison.Ordinal)])
            .ToArray();

    [Fact]
    public async Task PanelSizeSmallPutsPanelContentInTheFirstThreeRows()
    {
        var keys = Keys(await ListAsync("panel size small"));

        Assert.Contains("vr.mode", keys.Take(3));
        Assert.True(keys.Length <= 8);
    }

    [Fact]
    public async Task AQueryThatMatchesNothingDoesNotListEverything()
    {
        var listing = await ListAsync("zzzzqqqq");

        Assert.Empty(Keys(listing));
        Assert.Contains("zzzzqqqq", listing);
    }

    [Fact]
    public async Task NoQueryListsMoreThanAQueryDoes()
    {
        Assert.True(Keys(await ListAsync(null)).Length > 8);
    }
}
