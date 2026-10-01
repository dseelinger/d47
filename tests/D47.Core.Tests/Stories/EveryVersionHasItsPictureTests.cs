using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>Every cast member in two versions has both pictures, <c>assets/stories/&lt;story&gt;.&lt;cast&gt;.&lt;for-man|for-woman&gt;.png</c>.</summary>
public sealed class EveryVersionHasItsPictureTests
{
    private static string Pictures()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root is not null && !File.Exists(Path.Combine(root.FullName, "d47.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        return Path.Combine(root.FullName, "assets", "stories");
    }

    private static List<string> Missing(IEnumerable<StorySecret> secrets, string folder) =>
    [
        .. from secret in secrets
           from speaker in secret.Cast
           where speaker.Versions is not null
           from gender in new[] { CommanderGender.Man, CommanderGender.Woman }
           let picture = $"{secret.Speaker(speaker.Id, gender)!.Picture}.png"
           where !File.Exists(Path.Combine(folder, picture))
           select picture,
    ];

    [Fact]
    public void TheShippedCatalogHasEveryPicture() =>
        Assert.Empty(Missing(StoryCatalog.Default.Secrets, Pictures()));

    [Fact]
    public void AMissingPictureIsNamed()
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-story-pictures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            File.WriteAllBytes(Path.Combine(folder, $"{StoryFixtures.Id}.cray.for-man.png"), []);

            Assert.Equal([$"{StoryFixtures.Id}.cray.for-woman.png"], Missing([StoryFixtures.Versioned], folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
