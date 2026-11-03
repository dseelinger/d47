using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

/// <summary>The <c>mod_</c> prefix belongs to module tools, so the registry never falls through for a built-in name.</summary>
public class NoBuiltInToolNameStartsWithModTests
{
    [Fact]
    public void NoRegisteredToolNameStartsWithTheModulePrefix()
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;

        Assert.NotEmpty(registry.ToolNames);
        Assert.DoesNotContain(registry.ToolNames, name => name.StartsWith(ModuleTools.Prefix, StringComparison.Ordinal));
    }
}
