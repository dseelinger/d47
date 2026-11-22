using D47.Core.Viewing;
using Xunit;

namespace D47.Core.Tests.Viewing;

public class AVideoLicenceIsReadFromItsUrlTests
{
    [Theory]
    [InlineData("http://creativecommons.org/publicdomain/mark/1.0/", "public domain")]
    [InlineData("http://creativecommons.org/licenses/publicdomain/", "public domain")]
    [InlineData("https://creativecommons.org/publicdomain/zero/1.0/", "public domain")]
    [InlineData("https://creativecommons.org/licenses/by/4.0/", "CC BY 4.0")]
    [InlineData("http://creativecommons.org/licenses/by-nc-sa/3.0/", "CC BY-NC-SA 3.0")]
    [InlineData("https://creativecommons.org/licenses/by-nc-nd/4.0/", "CC BY-NC-ND 4.0")]
    [InlineData("http://creativecommons.org/licenses/by/3.0/us/", "CC BY 3.0")]
    [InlineData("http://creativecommons.org/licenses/by-sa", "CC BY-SA")]
    public void AFreeLicenceIsNamed(string url, string label) =>
        Assert.Equal(label, VideoLicence.Label(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("All rights reserved")]
    [InlineData("https://example.org/licenses/by/4.0/")]
    [InlineData("https://creativecommons.org.example.org/licenses/by/4.0/")]
    [InlineData("https://creativecommons.org/about/")]
    [InlineData("http://creativecommons.org/licenses/sampling+/1.0/")]
    [InlineData("ftp://creativecommons.org/licenses/by/4.0/")]
    public void AnythingElseIsNotFree(string? url) =>
        Assert.Null(VideoLicence.Label(url));
}
