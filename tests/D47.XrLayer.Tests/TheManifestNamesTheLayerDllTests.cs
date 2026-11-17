using System.Text.Json;
using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

/// <summary>The manifest beside the DLL, as the OpenXR loader reads it. Embedded from src, so no file is read.</summary>
public class TheManifestNamesTheLayerDllTests
{
    private const string LayerName = "XR_APILAYER_D47_panel";

    [Fact]
    public void ItIsAVersionOneManifest() =>
        Assert.Equal("1.0.0", Manifest().GetProperty("file_format_version").GetString());

    [Fact]
    public void ItNamesTheLayer() =>
        Assert.Equal(LayerName, Layer().GetProperty("name").GetString());

    [Fact]
    public void ItsLibraryIsTheLayerDllBesideIt()
    {
        var dll = typeof(LayerScope).Assembly.GetName().Name + ".dll";

        Assert.Equal(LayerName + ".dll", dll);
        Assert.Equal(@".\" + dll, Layer().GetProperty("library_path").GetString());
    }

    [Fact]
    public void ItCanBeTurnedOffWithAnEnvironmentVariable() =>
        Assert.Equal("DISABLE_XR_APILAYER_D47_PANEL", Layer().GetProperty("disable_environment").GetString());

    [Fact]
    public void ItHasNoEnableEnvironment() =>
        Assert.False(Layer().TryGetProperty("enable_environment", out _));

    [Fact]
    public void ItTargetsOpenXrOne()
    {
        Assert.Equal("1.0", Layer().GetProperty("api_version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(Layer().GetProperty("implementation_version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(Layer().GetProperty("description").GetString()));
    }

    private static JsonElement Layer() => Manifest().GetProperty("api_layer");

    private static JsonElement Manifest()
    {
        using var stream = typeof(TheManifestNamesTheLayerDllTests).Assembly.GetManifestResourceStream(LayerName + ".json")
            ?? throw new InvalidOperationException("The manifest is not embedded in the test assembly.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
}
