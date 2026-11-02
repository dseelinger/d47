using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The theme and size rows the capture tests draw.</summary>
public static class CaptureMatrix
{
    public static TheoryData<string, double, double> Rows { get; } = new()
    {
        { ThemeCatalog.Elite, 1280, 860 },
        { ThemeCatalog.Elite, 924, 640 },
        { ThemeCatalog.Elite, 512, 280 },
        { ThemeCatalog.Dark, 1280, 860 },
        { ThemeCatalog.Light, 1280, 860 },
        { ThemeCatalog.ElitePaletteId, 1280, 860 },
    };
}
