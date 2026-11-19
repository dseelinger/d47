using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The catalogue's filter as a sentence of tokens: "Show [status] kinds in [region]". The values live in the shared
/// <see cref="CodexCatalogueChoice"/>, so every catalogue page shows the same ones.
/// </summary>
internal sealed class CodexCatalogueFilters : TokenFilterBar<CodexCatalogueFilters.Kind>
{
    internal enum Kind
    {
        Status,
        Region,
    }

    private readonly CodexCatalogueChoice _choice;
    private readonly Func<int?> _here;

    public CodexCatalogueFilters(CodexCatalogueChoice choice, Func<int?> here)
        : base("CodexCatalogueToken.")
    {
        _choice = choice;
        _here = here;

        var sentence = new WrapPanel { Orientation = Orientation.Horizontal };
        sentence.Children.Add(Words("Show"));
        sentence.Children.Add(Token(Kind.Status, "Status"));
        sentence.Children.Add(Words("kinds in"));
        sentence.Children.Add(Token(Kind.Region, "Region"));

        Assemble(sentence, "CodexCatalogueClear");

        Refresh();
    }

    /// <summary>The region the counts are for: the Commander's when chosen and known, else null for every region.</summary>
    public int? Scope => _choice.Here && _here() is { } id ? id : null;

    /// <summary>Draws "N of M kinds" and the token values, and Clear filters while a value is off its default.</summary>
    public void Show(int shown, int total)
    {
        ShowCount(shown, total, "kinds");

        Refresh();
    }

    protected override void Clear()
    {
        _choice.Clear();
        Open = null;
        Refresh();
        RaiseChanged();
    }

    protected override void Update()
    {
        Values[Kind.Status].Text = CodexCatalogueModel.StatusLabels[(int)_choice.Status].ToUpperInvariant();
        Values[Kind.Region].Text = (Scope is { } id ? CodexModel.RegionName(id) : CodexCatalogueModel.EveryRegion).ToUpperInvariant();

        ClearShown = !_choice.IsDefault;
    }

    protected override Control PanelFor(Kind kind)
    {
        Segment segment;

        if (kind == Kind.Status)
        {
            segment = new Segment { ItemsSource = CodexCatalogueModel.StatusLabels, SelectedIndex = (int)_choice.Status };
            AutomationProperties.SetName(segment, "Status options");
        }
        else
        {
            var here = _here();
            var labels = new List<string> { CodexCatalogueModel.EveryRegion };
            var statuses = new List<ChoiceStatus?> { new(CodexCatalogueModel.EveryRegionStatus, ChoiceTone.Grey) };

            if (here is { } id && GalacticRegions.NameOf(id) is { } name)
            {
                labels.Add(name);
                statuses.Add(new ChoiceStatus(CodexCatalogueModel.HereStatus, ChoiceTone.Grey));
            }

            segment = new Segment
            {
                ItemsSource = labels,
                Statuses = statuses,
                SelectedIndex = Scope is null || labels.Count == 1 ? 0 : 1,
            };
            AutomationProperties.SetName(segment, "Region options");
        }

        segment.SelectionChanged += (_, _) =>
        {
            if (segment.SelectedIndex < 0)
            {
                return;
            }

            if (kind == Kind.Status)
            {
                _choice.Status = (CodexKindStatus)segment.SelectedIndex;
            }
            else
            {
                _choice.Here = segment.SelectedIndex == 1;
            }

            Open = null;
            Refresh();
            RaiseChanged();
        };

        return segment;
    }
}
