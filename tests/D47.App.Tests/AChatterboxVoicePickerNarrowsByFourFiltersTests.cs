using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

public class AChatterboxVoicePickerNarrowsByFourFiltersTests
{
    private static readonly string[] Genders = ["female", "male"];
    private static readonly string[] Pitches = ["low", "mid", "high"];
    private static readonly string[] Paces = ["slow", "even", "brisk"];

    /// <summary>Every gender, pitch and pace twice over, plus two custom voices.</summary>
    private static readonly VoiceInfo[] Voices =
    [
        .. Enumerable.Range(0, 2).SelectMany(copy =>
            from gender in Genders
            from pitch in Pitches
            from pace in Paces
            select new VoiceInfo($"{gender}-{pitch}-{pace}-{copy}", $"{gender} {pitch} {pace} {copy}", "en", gender)
            {
                Pitch = pitch,
                Pace = pace,
            }),
        new VoiceInfo("my-0badf00d", "Gale", "en", "female") { Custom = true, Pitch = "low", Pace = "even" },
        new VoiceInfo("my-0ddba11", "Hal", "en") { Custom = true, Pitch = "low", Pace = "even" },
    ];

    private static PickerRequest Request()
    {
        var settings = D47Settings.Defaults with
        {
            Speech = D47Settings.Defaults.Speech with { Provider = TtsProviderCatalog.ChatterboxId },
        };

        var row = SpeechCapability.Create(new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            Voices = _ => [.. Voices.Select(voice => voice.Id)],
            VoiceGender = (_, id) => Voices.Single(voice => voice.Id == id).Gender,
            VoicePitch = (_, id) => Voices.Single(voice => voice.Id == id).Pitch,
            VoicePace = (_, id) => Voices.Single(voice => voice.Id == id).Pace,
            VoiceCustom = (_, id) => Voices.Single(voice => voice.Id == id).Custom,
            VoiceClip = (_, id) => new ChatterboxClipState(
                Voices.Single(voice => voice.Id == id).Custom ? ChatterboxClip.Custom : ChatterboxClip.Shipped,
                0),
        }).Settings.Single(r => r.Key == SpeechCapability.VoiceKey);

        return new PickerRequest
        {
            Prompt = "Voice",
            Choices = [.. Voices.Select(voice => voice.Id)],
            Describe = id => Voices.Single(voice => voice.Id == id).Name,
            Facets = row.Facets!.Invoke(settings),
            ChoiceNoun = "voices",
        };
    }

    private static (PickerPage Picker, Window Window) Shown()
    {
        var picker = PickerPage.For(Request());
        var window = new Window { Content = picker, Width = 900, Height = 520 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (picker, window);
    }

    private static void Choose(PickerPage picker, string facet, int option)
    {
        picker.GetVisualDescendants().OfType<Segment>().Single(box => AutomationProperties.GetName(box) == facet)
            .GetVisualDescendants().OfType<RadioButton>().ElementAt(option).IsChecked = true;

        Dispatcher.UIThread.RunJobs();
    }

    private static string Count(PickerPage picker) =>
        string.Concat(picker.GetControl<TextBlock>("MatchCount").Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text));

    [AvaloniaFact]
    public void TheCountSaysHowManyOfTheWholeListMatch()
    {
        var (picker, window) = Shown();

        Assert.Equal($"{Voices.Length} OF {Voices.Length} VOICES", Count(picker));

        Choose(picker, "Gender", 1);
        Choose(picker, "Pitch", 1);
        Choose(picker, "Pace", 2);

        // Two catalogue voices and the custom Gale.
        Assert.Equal($"3 OF {Voices.Length} VOICES", Count(picker));

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("voice-picker-four-filters.png");
    }

    [AvaloniaFact]
    public void TheFiltersAndTheNameBoxBothApply()
    {
        var (picker, _) = Shown();

        Choose(picker, "Gender", 1);
        Choose(picker, "Pitch", 1);
        Choose(picker, "Pace", 2);
        picker.GetControl<TextBox>("FilterBox").Text = "gale";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal($"1 OF {Voices.Length} VOICES", Count(picker));
    }

    [AvaloniaFact]
    public void ShowCustomListsOnlyTheCustomVoices()
    {
        var (picker, _) = Shown();

        Choose(picker, "Show", 2);

        Assert.Equal($"2 OF {Voices.Length} VOICES", Count(picker));
    }

    [AvaloniaFact]
    public void AnEmptyListNamesTheFilterThatSettingBackToAnyWouldFill()
    {
        var (picker, _) = Shown();

        // Male, Mid and Brisk match two catalogue voices, and Show Custom leaves only Gale and Hal. Setting
        // Gender, Pitch or Pace back to Any still lists nothing; only Show helps.
        Choose(picker, "Gender", 2);
        Choose(picker, "Pitch", 2);
        Choose(picker, "Pace", 3);
        Choose(picker, "Show", 2);

        Assert.Equal("Nothing matches. Set Show back to All.", picker.GetControl<TextBlock>("EmptyHint").Text);
    }

    [AvaloniaFact]
    public void EveryOptionOfEveryFacetIsDrawnInsideThePage()
    {
        var (picker, _) = Shown();

        foreach (var box in picker.GetVisualDescendants().OfType<Segment>().Where(box => AutomationProperties.GetName(box) is { Length: > 0 }))
        {
            foreach (var option in box.GetVisualDescendants().OfType<RadioButton>())
            {
                var right = option.TranslatePoint(new Point(option.Bounds.Width, 0), picker)!.Value.X;

                Assert.True(option.DesiredSize.Width <= option.Bounds.Width + 0.5, $"{option.Content} is squeezed.");
                Assert.True(right <= picker.Bounds.Width + 0.5, $"{AutomationProperties.GetName(box)} {option.Content} ends at {right}.");
            }
        }
    }
}
