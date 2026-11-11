using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Globalization;
using Avalonia.Automation;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex › a region › an entry › Crop: a square cut from a capture, saved as the entry's picture.</summary>
public sealed class CodexCropPage : UserControl, IPageSummary
{
    private readonly CodexPictureKey _key;
    private readonly CodexPictureSet _pictures;
    private readonly PanelNavigator _nav;
    private readonly CodexPictureWork _work;
    private readonly bool _kept;
    private Button _save = new();
    private TextBlock _readout = new();
    private TextBlock _floor = new();
    private TextBlock _status = new();
    private byte[]? _source;
    private CodexCropPreview? _preview;
    private int _openStep;
    private int _step;
    private Amount _amount = new();
    private ScaleTrack _scale = new("", "", 0, 1);
    private Button _reset = new();

    /// <summary>Cuts from the kept source with its saved square when <paramref name="kept"/>, else from the capture in <paramref name="work"/>.</summary>
    public CodexCropPage(CodexPictureKey key, CodexPictureSet pictures, PanelNavigator nav, CodexPictureWork work, bool kept)
    {
        _key = key;
        _pictures = pictures;
        _nav = nav;
        _work = work;
        _kept = kept;

        Margin = new Thickness(14);
    }

    internal CropSurface? Surface { get; private set; }

    /// <summary>The panel keeps a level once built, so the capture is read again each time the page is shown.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Load();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Content = null;
        Surface = null;
        _source = null;
        _preview = null;
    }

    public string Summary => CodexPage.SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>Writes the picture, its source and its square, then returns to the entry page.</summary>
    internal async Task SaveAsync()
    {
        if (Surface is null || _source is null)
        {
            return;
        }

        var square = Surface.Square;
        var key = _key;
        var source = _source;
        var gamma = Gamma;
        var store = _pictures.Store;

        _save.IsEnabled = false;
        _status.Text = string.Empty;

        try
        {
            var saved = await Task.Run(() =>
            {
                if (CodexPictureCutter.Cut(source, square, gamma) is not { } picture)
                {
                    return false;
                }

                store.SaveLocal(key, picture, source, square, gamma);

                return true;
            });

            if (!saved)
            {
                _status.Text = CodexModel.CaptureGoneText;
                _save.IsEnabled = true;

                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"The picture could not be saved: {ex.Message}";
            _save.IsEnabled = true;

            return;
        }

        if (!_kept)
        {
            _pictures.RaisePictureSaved(key);
        }

        Leave();
    }

    private double? Gamma => _step == 0 ? null : PictureLift.GammaOf(_step);

    private async void Load()
    {
        _source = _kept ? _pictures.Store.LocalSource(_key) : _work.Capture is { } path ? _pictures.CaptureBytes(path) : null;
        var gamma = _kept ? _pictures.Store.LocalGamma(_key) : _work.Capture is { } captured ? _pictures.CaptureGamma(captured) : null;
        _openStep = PictureLift.StepOf(gamma);
        _step = _openStep;

        var source = _source;
        CodexCropPreview? preview = null;

        if (source is not null && _openStep != 0)
        {
            Content = CreditsKit.Message(CodexModel.BrighteningText);
            preview = await Task.Run(() => CodexCropPreview.Create(source));

            if (!ReferenceEquals(source, _source))
            {
                return;
            }
        }
        else if (source is not null)
        {
            preview = CodexCropPreview.Create(source);
        }

        Build(preview);
    }

    private void Build(CodexCropPreview? preview)
    {
        _save = new Button { Name = "CodexCropSave", Content = "Save" };
        _save.Click += async (_, _) => await SaveAsync();
        _readout = CreditsKit.Mono(string.Empty, TypeScale.Small, ThemeManager.GreyKey, right: false);
        _floor = CreditsKit.Mono(CodexModel.SmallestSquareText.ToUpperInvariant(), TypeScale.Small, ThemeManager.GreyKey, right: false);
        _status = RoutingKit.Ink(string.Empty, TypeScale.Small, ThemeManager.GreyKey, wrap: true);

        var body = new StackPanel { Spacing = 10 };
        var cancel = new Button { Name = "CodexCropCancel", Content = "Cancel" };
        cancel.Click += (_, _) => Leave();

        if (preview is not null)
        {
            _preview = preview;
            var saved = _kept ? _pictures.Store.LocalCrop(_key) : null;
            Surface = new CropSurface(preview.At(Gamma), preview.Width, preview.Height, saved ?? CropSquare.Start(preview.Width, preview.Height));
            Surface.SquareChanged += _ => Readout();

            body.Children.Add(new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = Surface,
            });
            body.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Children = { _readout, _floor },
            });
            body.Children.Add(BrightnessRow());
            Readout();
            _save.IsEnabled = true;
        }
        else
        {
            body.Children.Add(CreditsKit.Message(CodexModel.CaptureGoneText));
            _save.IsEnabled = false;
        }

        body.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            Children = { _save, cancel },
        });
        body.Children.Add(_status);

        Content = LoadoutPages.Scrolling(body);
    }

    private void Readout()
    {
        var square = Surface!.Square;

        _readout.Text = CodexModel.CropReadout(square);
        _floor.IsVisible = square.Side <= Math.Min(CropSquare.Floor, Math.Min(Surface.PictureWidth, Surface.PictureHeight));
    }

    /// <summary>Goes back to the entry page, through the choice when the crop came from it.</summary>
    private void Leave()
    {
        for (var i = 0; i < 3 && _nav.Trail.Count > 0 && !CodexPage.IsEntry(_nav.Trail[^1].Key); i++)
        {
            _nav.Back();
        }
    }

    private Control BrightnessRow()
    {
        var label = CreditsKit.Mono("BRIGHTNESS", TypeScale.Small, ThemeManager.GreyKey, right: false);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Margin = new Thickness(0, 0, 12, 0);

        _amount = new Amount
        {
            Name = "CodexCropBrightness",
            Step = 1,
            Format = StepFormat,
            Minimum = PictureLift.DarkestStep,
            Maximum = PictureLift.BrightestStep,
            DisablesAtEnds = true,
        };
        _scale = new ScaleTrack("Darker", "Brighter", PictureLift.DarkestStep, PictureLift.BrightestStep) { Margin = new Thickness(12, 0, 0, 0) };
        AutomationProperties.SetName(_scale, "Brightness level");
        _reset = SettingsView.ResetGlyph("CodexCropBrightnessReset", $"Back to {OpenStepText}");
        _reset.Click += (_, _) => SetStep(_openStep);
        _amount.ValueCommitted += (_, _) => SetStep(_amount.Value is { } shown ? (int)shown : _step);
        _scale.ValueChanged += (_, _) => SetStep(_scale.Value);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        Grid.SetColumn(_amount, 1);
        Grid.SetColumn(_scale, 2);
        Grid.SetColumn(_reset, 3);
        row.Children.Add(label);
        row.Children.Add(_amount);
        row.Children.Add(_scale);
        row.Children.Add(_reset);
        ShowStep();

        return row;
    }

    private const string StepFormat = "+0;-0;0";

    private string OpenStepText => _openStep.ToString(StepFormat, CultureInfo.InvariantCulture);

    private void SetStep(int step)
    {
        _step = Math.Clamp(step, PictureLift.DarkestStep, PictureLift.BrightestStep);
        ShowStep();

        if (_preview is { } preview)
        {
            Surface?.ShowPicture(preview.At(Gamma));
        }
    }

    private void ShowStep()
    {
        _amount.Value = _step;
        _scale.Show(_step);
        _reset.IsVisible = _step != _openStep;
    }
}
