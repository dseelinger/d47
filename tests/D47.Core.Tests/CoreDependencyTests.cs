using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// Core's zero-dependency rule is what makes the replay harness possible
/// (architecture.md §3, §8). Asserted here so a stray reference fails the build rather
/// than being discovered when someone tries to run Core with no audio device.
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
        "Serilog.Sinks", // concrete log targets; logging abstractions are fine
    ];

    [Fact]
    public void CoreReferencesNoUiHardwareOrProviderAssemblies()
    {
        var violations = CoreAssembly.Reference
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => Forbidden.Any(f => name.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"D47.Core must not reference UI, hardware or provider assemblies. Found: {string.Join(", ", violations)}");
    }
}
