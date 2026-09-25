using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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
/// UPDATE and DELETE on the preset row, and UNDO in the notice they show (#482): calling #237's
/// <see cref="GuardianPresets.Update"/> and <see cref="GuardianPresets.Delete"/> straight away and
/// restoring the settings from before the action.
/// </summary>
public sealed class UpdateAndDeleteVoicePresetsWithUndoTests
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

    private static Button Named(SettingsView view, string name) =>
        Page(view).GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static Button Update(SettingsView view) => Named(view, SettingsView.GuardianUpdateName);

    private static Button Delete(SettingsView view) => Named(view, SettingsView.GuardianDeleteName);

    private static Button Undo(SettingsView view) => Named(view, SettingsView.GuardianUndoName);

    private static Border Notice(SettingsView view) =>
        Page(view).GetVisualDescendants().OfType<Border>().Single(border => border.Name == SettingsView.GuardianNoticeName);

    private static string NoticeText(SettingsView view) =>
        Notice(view).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "GuardianNoticeText").Text ?? "";

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private static void PickSaved(SettingsView view, string label)
    {
        Press(Named(view, InlinePicker.ButtonName));
        Press(view.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass)
                && Avalonia.Automation.AutomationProperties.GetName(button) == label));
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

    private static CheckBox Box(SettingsView view, string id) =>
        (CheckBox)view.ControlFor(SpeechCapability.GuardianEffectKey(id))!;

    [AvaloniaFact]
    public void UpdateOnlyShowsWhenCustomIsChangedFromASavedPreset()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        PickSaved(host.View, "Hull breach");
        Assert.False(Update(host.View).IsVisible);

        Box(host.View, "cylon").IsChecked = true;
        Jobs();

        Assert.Equal("custom", settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.True(Update(host.View).IsVisible);
        Assert.False(Delete(host.View).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void UpdateWritesTheCurrentEffectsIntoTheBasisAndShowsTheNotice()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        PickSaved(host.View, "Hull breach");
        Box(host.View, "cylon").IsChecked = true;
        Jobs();

        Press(Update(host.View));

        Assert.Equal("saved:Hull breach", settings.Read(SpeechCapability.GuardianPresetKey));
        var saved = settings.Current.Speech.GuardianVoice.SavedPresets!.Single();
        Assert.True(saved.Effects.Single(e => e.Id == "cylon").Ticked);
        Assert.True(saved.Effects.Single(e => e.Id == "reverb").Ticked);
        Assert.Equal("Updated Hull breach with the current effects.", NoticeText(host.View));
        Assert.True(Undo(host.View).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void UndoAfterUpdateRestoresThePresetsPreviousEffects()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        PickSaved(host.View, "Hull breach");
        Box(host.View, "cylon").IsChecked = true;
        Jobs();
        Press(Update(host.View));

        Press(Undo(host.View));

        var saved = settings.Current.Speech.GuardianVoice.SavedPresets!.Single();
        Assert.False(saved.Effects.Single(e => e.Id == "cylon").Ticked);
        Assert.True(saved.Effects.Single(e => e.Id == "reverb").Ticked);
        Assert.Equal("custom", settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.Equal("Hull breach", settings.Current.Speech.GuardianVoice.Basis);
        Assert.False(Notice(host.View).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void DeleteRemovesThePresetAndShowsTheNoticeWithUndo()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        PickSaved(host.View, "Hull breach");
        Press(Delete(host.View));

        Assert.Empty(settings.Current.Speech.GuardianVoice.SavedPresets!);
        Assert.Equal("custom", settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.Null(settings.Current.Speech.GuardianVoice.Basis);
        Assert.Equal("Deleted Hull breach.", NoticeText(host.View));
        Assert.True(Undo(host.View).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void UndoAfterDeleteRestoresThePresetAndTheBasis()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        PickSaved(host.View, "Hull breach");
        Press(Delete(host.View));

        Press(Undo(host.View));

        Assert.Equal("Hull breach", settings.Current.Speech.GuardianVoice.SavedPresets!.Single().Name);
        Assert.Equal("saved:Hull breach", settings.Read(SpeechCapability.GuardianPresetKey));
        Assert.Equal("Hull breach", settings.Current.Speech.GuardianVoice.Basis);
        Assert.False(Notice(host.View).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void UndoIsGoneOnceAnotherActionOpensTheNameRow()
    {
        var (host, settings) = OpenVoice();

        Box(host.View, "reverb").IsChecked = true;
        Jobs();
        Press(Named(host.View, SettingsView.GuardianSaveAsName));
        var field = Page(host.View).GetVisualDescendants().OfType<TextBox>()
            .Single(box => box.Name == SettingsView.GuardianNameFieldName);
        field.Text = "Hull breach";
        Jobs();
        Press(Named(host.View, SettingsView.GuardianNameActionName));

        PickSaved(host.View, "Hull breach");
        Box(host.View, "cylon").IsChecked = true;
        Jobs();
        Press(Update(host.View));
        Assert.True(Notice(host.View).IsVisible);

        Press(Named(host.View, SettingsView.GuardianRenameName));

        Assert.False(Notice(host.View).IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void SaveAndRenameNoticesCarryNoUndo()
    {
        var (host, settings) = OpenVoice(arrange: s => WithSaved(s, "Hull breach", "reverb", 3));

        PickSaved(host.View, "Hull breach");
        Press(Named(host.View, SettingsView.GuardianRenameName));

        var field = Page(host.View).GetVisualDescendants().OfType<TextBox>()
            .Single(box => box.Name == SettingsView.GuardianNameFieldName);
        field.Text = "Reactor whine";
        Jobs();
        Press(Named(host.View, SettingsView.GuardianNameActionName));

        Assert.Equal("Renamed Hull breach to Reactor whine.", NoticeText(host.View));
        Assert.False(Undo(host.View).IsVisible);

        host.Close();
    }
}
