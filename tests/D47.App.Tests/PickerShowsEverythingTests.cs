using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The picker opens showing everything it can offer, in words a person can read.
/// <para>
/// Reported from a running build, on the microphone row: the box at the top held
/// <c>{0.0.1.00000000}.{a711ffd8-c2b5-40e8-b07e-7cd27a99045e}</c> and the list below it had one
/// entry. Both had the same cause — the box was pre-filled with the current value, the current
/// value of a device row is an id, and the box is also the filter — so every other microphone on
/// the machine was hidden behind text the Commander never typed, by a string they could not read.
/// </para>
/// </summary>
public class PickerShowsEverythingTests
{
    private const string Selected = "{0.0.1.00000000}.{a711ffd8-c2b5-40e8-b07e-7cd27a99045e}";
    private const string Other = "{0.0.1.00000000}.{b9330000-0000-0000-0000-000000000000}";

    private static PickerRequest Devices() => new()
    {
        Prompt = "Microphone",
        Choices = [Selected, Other],
        Describe = id => id == Selected ? "Microphone (Logi 4K Stream Edition)" : "Microphone (Virtual Desktop Audio)",
        Current = Selected,
        DefaultDisplay = "the system default — Microphone (Virtual Desktop Audio)",
    };

    [AvaloniaFact]
    public void EveryChoiceIsListedAndTheIdIsNotInTheBox()
    {
        var picker = PickerWindow.For(Devices());

        var filter = picker.GetControl<TextBox>("FilterBox");
        var choices = picker.GetControl<ListBox>("Choices");

        Assert.True(string.IsNullOrEmpty(filter.Text), $"the filter box opened holding \"{filter.Text}\".");
        Assert.Equal(2, choices.ItemCount);

        // Readable, not the stored value. The list is what the Commander picks from, so it is
        // the one place the id must never be.
        Assert.Equal(
            ["Microphone (Logi 4K Stream Edition)", "Microphone (Virtual Desktop Audio)"],
            choices.ItemsSource!.Cast<string>());

        // And they are actually on screen, not merely in the ItemsSource. The reported symptom
        // was a list with one row in it, so the assertion worth having is the one about rows.
        picker.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, choices.GetVisualDescendants().OfType<ListBoxItem>().Count());

        picker.Close();
    }

    /// <summary>
    /// Nothing typed still means "keep what I had", which is what makes an empty box safe: the
    /// current value is selected instead of being spelled out.
    /// </summary>
    [AvaloniaFact]
    public void TheCurrentValueIsSelectedRatherThanTyped()
    {
        var picker = PickerWindow.For(Devices());

        Assert.Equal(0, picker.GetControl<ListBox>("Choices").SelectedIndex);
    }

    /// <summary>
    /// The default button is the width of the list and trims what does not fit, with the whole
    /// string on the pointer. In one right-aligned row with Cancel and Use this, a resolved
    /// device name pushed both of those off the left edge of a fixed-width window — a picker
    /// with no visible way out.
    /// </summary>
    [AvaloniaFact]
    public void TheDefaultButtonTrimsInsteadOfPushingTheOtherButtonsOffScreen()
    {
        var picker = PickerWindow.For(Devices());
        picker.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var button = picker.GetControl<Button>("DefaultButton");
        var label = picker.GetControl<TextBlock>("DefaultButtonText");

        Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);
        Assert.Equal(
            "Use the default (the system default — Microphone (Virtual Desktop Audio))",
            ToolTip.GetTip(button));

        // Both of the other buttons are still on screen, which is the failure this row shape
        // exists to prevent.
        var buttons = picker.GetVisualDescendants().OfType<Button>().ToList();

        Assert.Contains(buttons, b => b.Content as string == "Cancel");
        Assert.Contains(buttons, b => b.Content as string == "Use this");

        picker.Close();
    }
}
