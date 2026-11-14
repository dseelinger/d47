using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>A slot page's stepper draws a label only when its step carries one (#2423).</summary>
public class AStepperLabelsOnlyWhatItIsGivenTests
{
    private static List<string> Drawn(LoadoutStep step)
    {
        var line = new LoadoutLine("Priority group 1", LoadoutTone.Body) { Step = step };
        var window = KitWindow.ShowInStrip(LoadoutPages.Stepped(line, null));

        try
        {
            return [.. window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? "")];
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ThePriorityGroupStepperDrawsItsButtonsAndNoGrade()
    {
        var texts = Drawn(new LoadoutStep(1, [5, 4, 3, 2, 1], _ => { }));

        Assert.Contains("▲", texts);
        Assert.Contains("▼", texts);
        Assert.DoesNotContain(texts, text => text.StartsWith("Grade", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void AGradeStepperDrawsGradeAndItsValue()
    {
        var texts = Drawn(new LoadoutStep(3, [5, 4, 3, 2, 1], _ => { }, "Grade"));

        Assert.Contains("Grade 3", texts);
    }
}
