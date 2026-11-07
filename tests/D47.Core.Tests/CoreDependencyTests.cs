using D47.Core.Ticking;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// Core's zero-dependency rule is what makes the replay harness possible.
/// </summary>
public class CoreDependencyTests
{
    private static readonly string[] Forbidden =
    [
        "Avalonia",      // UI
        "Vortice",       // D3D11 interop
        "NAudio",        // audio hardware
        "Whisper",       // STT
        "Anthropic",     // LLM providers
        "OpenAI",
        "ModelContextProtocol", // module hosting
        "Serilog.Sinks", // concrete log targets; logging abstractions are fine
    ];

    [Fact]
    public void CoreReferencesNoUiHardwareOrProviderAssemblies()
    {
        var violations = typeof(TickLoop).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => Forbidden.Any(f => name.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"D47.Core must not reference UI, hardware or provider assemblies. Found: {string.Join(", ", violations)}");
    }
}
