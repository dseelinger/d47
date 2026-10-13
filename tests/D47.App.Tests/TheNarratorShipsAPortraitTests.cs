using Avalonia.Media.Imaging;
using Avalonia.Headless.XUnit;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Narrator's shipped portrait exists and decodes at 512 by 512 (#1141).</summary>
[Trait("Category", "Integration")]
public sealed class TheNarratorShipsAPortraitTests
{
    [AvaloniaFact]
    public void TheNarratorsPortraitDecodesAt512By512()
    {
        var path = Path.Combine(AppSource.RepositoryRoot(), "assets", "portraits", SpeakerPictures.Narrator + ".jpg");

        Assert.True(File.Exists(path), "assets/portraits/narrator.jpg is missing");

        using var stream = File.OpenRead(path);
        using var bitmap = new Bitmap(stream);

        Assert.Equal(512, bitmap.PixelSize.Width);
        Assert.Equal(512, bitmap.PixelSize.Height);
    }
}
