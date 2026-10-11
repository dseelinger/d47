using Xunit;

namespace D47.App.Tests;

public sealed class TheLogbookPageChoosesDatesOnAFullPageTests
{
    [Fact]
    [Trait("Category", "Gate")]
    public void TheLogbookPageHoldsNoDatePickerAndNoPopup()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        var source = File.ReadAllText(Path.Combine(directory!.FullName, "src", "D47.App", "Controls", "LogbookPage.cs"));

        Assert.DoesNotContain("DatePicker", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Popup", source, StringComparison.Ordinal);
        Assert.Contains("prompts.Pick(", source, StringComparison.Ordinal);
    }
}
