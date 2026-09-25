using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>
/// The preset buttons after TEST, the name row and the notice: saving a new preset and renaming a
/// saved one, calling #237's <see cref="GuardianPresets.Save"/> and <see cref="GuardianPresets.Rename"/>
/// and showing the sentence they return (#481).
/// </summary>
public sealed class SaveAndRenameYourOwnVoicePresetsTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static (SettingsHost Host, SettingsService Settings) OpenVoice(Action<SettingsService>? arrange = null)
    {
        var (settings, viewState, paths, _, _) = TestSurface.CreateFull();
        arrange?.Invoke(settings);
        var host = SettingsHost.Open(settings, viewState, paths, width: 1180, height: 880);
        Open(host.View, "voice");
        return (host, settings);
    }

    private static InlinePicker Picker(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<InlinePicker>().Single();

    private static Button Named(SettingsView view, string name) =>
        Page(view).GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static Button SaveAs(SettingsView view) => Named(view, SettingsView.GuardianSaveAsName);

    private static Button Rename(SettingsView view) => Named(view, SettingsView.GuardianRenameName);

    private static Button NameAction(SettingsView view) => Named(view, SettingsView.GuardianNameActionName);

    private static Button NameCancel(SettingsView view) => Named(view, SettingsView.GuardianNameCancelName);

    private static TextBox NameField(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == SettingsView.GuardianNameFieldName);

    private static TextBlock NameMessage(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == SettingsView.GuardianNameMessageName);

    private static StackPanel NameRow(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == SettingsView.GuardianNameRowName);

    private static TextBlock NameLabel(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == SettingsView.GuardianNameLabelName);

    private static Border Notice(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<Border>().Single(border => border.Name == SettingsView.GuardianNoticeName);

    private static string NoticeText(SettingsView view) =>
        Notice(view).GetVisualDescendants().OfType<TextBlock>().Single().Text ?? "";

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private static void Type(TextBox box, string text)
    {
        box.Text = text;
        Jobs();
    }

    private static void WithSaved(SettingsService settings, string name, string tickedId, int level)
    {
        var effects = GuardianVoice.Table
            .Select(effect => new GuardianVoiceEffect
            {
                Id = effect.Id,
                Ticked = effect.Id == tickedId,
                Level = effect.Id == tickedId ? level : effect.DefaultLevel,
            })
            .ToList();

        settings.Replace("test preset", s => s with
        {
            Speech = s.Speech with
            {
                GuardianVoice = s.Speech.GuardianVoice with
                {
                    SavedPresets = [new GuardianVoicePreset { Name = name, Effects = effects }],
                },
            },
        });
    }

    [AvaloniaFact]
    public void SaveAsIsDisabledOnEveryBuiltInAndEnabledOnCustom()
    {
        var (host, settings) = OpenVoice();

        Assert.Equal("off", settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.True(SaveAs(host.View).IsVisible);
        Assert.False(SaveAs(host.View).IsEnabled);

        Box(host.View, "reverb").IsChecked = true;
        Jobs();

        Assert.Equal("custom", settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.True(SaveAs(host.View).IsVisible);
        Assert.True(SaveAs(host.View).IsEnabled);

        host.Close();
    }

    [AvaloniaFact]
    public void OneOfYourPresetsUnchangedShowsRenameNotSaveAs()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        Press(Named(host.View, InlinePicker.ButtonName));
        Press(host.View.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass)
                && Avalonia.Automation.AutomationProperties.GetName(button) == "Hull breach"));

        Assert.True(Rename(host.View).IsVisible);
        Assert.False(SaveAs(host.View).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void SaveWithAValidNameAddsThePresetAndShowsTheNotice()
    {
        var (host, settings) = OpenVoice();

        Box(host.View, "reverb").IsChecked = true;
        Jobs();

        Press(SaveAs(host.View));
        Assert.False(Picker(host.View).IsOpen);
        Assert.True(NameRow(host.View).IsVisible);
        Assert.Equal("Preset name", NameLabel(host.View).Text);

        Type(NameField(host.View), "Hull breach");
        Press(NameAction(host.View));

        Assert.False(NameRow(host.View).IsVisible);
        Assert.Contains(
            settings.Current.Speech.GuardianVoice.SavedPresets!,
            preset => preset.Name == "Hull breach");
        Assert.Equal("saved:Hull breach", settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.Equal("Hull breach", settings.Current.Speech.GuardianVoice.Basis);
        Assert.True(Notice(host.View).IsVisible);
        Assert.Equal("Saved Hull breach.", NoticeText(host.View));

        host.Close();
    }

    [AvaloniaFact]
    public void RenameChangesTheNameInTheListAndThePickerAndShowsTheNotice()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        Press(Named(host.View, InlinePicker.ButtonName));
        Press(host.View.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass)
                && Avalonia.Automation.AutomationProperties.GetName(button) == "Hull breach"));

        Press(Rename(host.View));
        Assert.Equal("New name", NameLabel(host.View).Text);
        Assert.Equal("Hull breach", NameField(host.View).Text);

        Type(NameField(host.View), "Reactor whine");
        Press(NameAction(host.View));

        Assert.Equal("Reactor whine", settings.Current.Speech.GuardianVoice.SavedPresets!.Single().Name);
        Assert.Equal("saved:Reactor whine", settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.Equal("Reactor whine", settings.Current.Speech.GuardianVoice.Basis);
        Assert.Equal("Renamed Hull breach to Reactor whine.", NoticeText(host.View));

        host.Close();
    }

    [AvaloniaFact]
    public void ARefusalFromCoreShowsInRedAndSavesNothing()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        Box(host.View, "cylon").IsChecked = true;
        Jobs();

        Press(SaveAs(host.View));
        Type(NameField(host.View), "Hull breach");
        Press(NameAction(host.View));

        Assert.True(NameRow(host.View).IsVisible);
        Assert.True(NameMessage(host.View).IsVisible);
        Assert.Equal("A preset with that name already exists.", NameMessage(host.View).Text);
        Assert.Single(settings.Current.Speech.GuardianVoice.SavedPresets!);

        host.Close();
    }

    [AvaloniaFact]
    public void EnterConfirmsAndEscClosesTheRowWithoutWriting()
    {
        var (host, settings) = OpenVoice();

        Box(host.View, "reverb").IsChecked = true;
        Jobs();
        Press(SaveAs(host.View));

        Type(NameField(host.View), "Hull breach");
        NameField(host.View).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Jobs();

        Assert.False(NameRow(host.View).IsVisible);
        Assert.Null(settings.Current.Speech.GuardianVoice.SavedPresets);

        Press(SaveAs(host.View));
        Type(NameField(host.View), "Hull breach");
        NameField(host.View).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Jobs();

        Assert.False(NameRow(host.View).IsVisible);
        Assert.Single(settings.Current.Speech.GuardianVoice.SavedPresets!);

        host.Close();
    }

    [AvaloniaFact]
    public void CancelClosesTheRowWithoutWriting()
    {
        var (host, settings) = OpenVoice();

        Box(host.View, "reverb").IsChecked = true;
        Jobs();
        Press(SaveAs(host.View));
        Type(NameField(host.View), "Hull breach");

        Press(NameCancel(host.View));

        Assert.False(NameRow(host.View).IsVisible);
        Assert.Null(settings.Current.Speech.GuardianVoice.SavedPresets);

        host.Close();
    }

    [AvaloniaFact]
    public void ASecondActionWithinSixSecondsReplacesTheNotice()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        Press(Named(host.View, InlinePicker.ButtonName));
        Press(host.View.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass)
                && Avalonia.Automation.AutomationProperties.GetName(button) == "Hull breach"));

        Press(Rename(host.View));
        Type(NameField(host.View), "Reactor whine");
        Press(NameAction(host.View));
        Assert.Equal("Renamed Hull breach to Reactor whine.", NoticeText(host.View));

        Press(Rename(host.View));
        Type(NameField(host.View), "Ion storm");
        Press(NameAction(host.View));

        Assert.Equal("Renamed Reactor whine to Ion storm.", NoticeText(host.View));

        host.Close();
    }

    private static CheckBox Box(SettingsView view, string id) =>
        (CheckBox)view.ControlFor(SpeechCapability.GuardianEffectKey(id))!;
}
