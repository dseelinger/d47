using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Configuration;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>Leaving a page with the cursor in a text row's box saves what was typed.</summary>
public sealed class ATypedValueSurvivesAPageChangeTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void ATypedValueIsKeptWhenAnotherPlaceOpens()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);
        var view = host.View;

        var ids = view.SectionIds;
        TextBox? box = null;
        var place = -1;

        for (var i = 0; i < ids.Count && box is null; i++)
        {
            view.ShowPlace(i);
            Jobs();
            box = Page(view).GetVisualDescendants().OfType<TextBox>()
                // A key box applies on its own button, not on losing focus.
                .FirstOrDefault(candidate => candidate.IsEffectivelyVisible && !candidate.AcceptsReturn
                    && !string.Equals(candidate.PlaceholderText, "Paste a key to store it", StringComparison.Ordinal));
            place = i;
        }

        Assert.NotNull(box);

        box.Focus();
        Jobs();
        Assert.True(box.IsFocused, "the box must hold focus for the page change to detach it");

        box.Text = "typed-in-a-box";

        view.ShowPlace(place == 0 ? 1 : 0);
        Jobs();

        var kept = settings.Sections
            .SelectMany(section => section.Rows)
            .Any(row => settings.Read(row.Key) == "typed-in-a-box");

        Assert.True(kept, "the typed value must be applied when the box loses focus ");

        host.Close();
    }
}
