namespace D47.Core.Modules;

/// <summary>What a part of a module page means, which d47 turns into a colour.</summary>
public enum ModuleTone
{
    Value,
    Name,
    Here,
    Stored,
    Warning,
    Error,
}

/// <summary>A module page as d47 draws it: a run of parts, each from the fixed component set.</summary>
public sealed record ModulePageContent(IReadOnlyList<ModulePart> Parts);

public abstract record ModulePart;

public sealed record ModuleGroupHead(string Title, string? Description) : ModulePart;

/// <summary>A grid of up to three labelled values, or a line of prose when <see cref="Prose"/> is set.</summary>
public sealed record ModuleDataBlock(IReadOnlyList<ModuleDatum> Cells, string? Prose, ModuleTone? Tone) : ModulePart;

public sealed record ModuleDatum(string Label, string Value, ModuleTone? Tone);

/// <summary>A warning or error notice; a page holds at most two.</summary>
public sealed record ModulePageNotice(ModuleTone Tone, string Label, string Text) : ModulePart;

/// <summary>A labelled row holding one control.</summary>
public sealed record ModuleSettingsRow(string Label, ModuleRowControl Control) : ModulePart;

public abstract record ModuleRowControl;

public sealed record ModuleStatusBlock(string Text, ModuleTone? Tone, ModuleTileButton? Button) : ModuleRowControl;

/// <summary>A Segmented when <see cref="Stepper"/> is false, else a NumberStepper.</summary>
public sealed record ModuleChoice(bool Stepper, ModuleSends Sends, IReadOnlyList<ModuleOption> Options, int Selected) : ModuleRowControl;

/// <param name="ValueJson">The JSON value sent when the option is chosen.</param>
public sealed record ModuleOption(string Label, string ValueJson);

/// <summary>A CheckboxTile grid of two or four columns.</summary>
public sealed record ModuleCheckboxTiles(int Columns, IReadOnlyList<ModuleCheckboxTile> Tiles) : ModulePart;

public sealed record ModuleCheckboxTile(string Label, bool Checked, ModuleSends Sends);

/// <summary>ListBoxItem rows under an optional list head.</summary>
public sealed record ModuleList(string? Head, IReadOnlyList<ModuleListRow> Rows) : ModulePart;

/// <param name="Opens">Another page of the same module, or null for a row that opens nothing.</param>
public sealed record ModuleListRow(string Name, string? Sub, string? Aside, string? Opens);

/// <summary>TileButtons in one row.</summary>
public sealed record ModuleTileButtons(IReadOnlyList<ModuleTileButton> Buttons) : ModulePart;

/// <summary>A tile that calls one of the module's own tools, or opens another of its pages.</summary>
public sealed record ModuleTileButton(string Label, bool Destructive, ModuleToolCall? Calls, string? Opens);

/// <param name="ArgumentsJson">A JSON object.</param>
public sealed record ModuleToolCall(string Tool, string ArgumentsJson);

/// <summary>A control that calls <see cref="Tool"/> with its value as <see cref="Argument"/>.</summary>
public sealed record ModuleSends(string Tool, string Argument);

public sealed record ModuleTable(IReadOnlyList<ModuleColumn> Columns, IReadOnlyList<IReadOnlyList<ModuleCell>> Rows) : ModulePart;

/// <param name="Numeric">Drawn mono and right-aligned.</param>
public sealed record ModuleColumn(string Label, bool Numeric);

public sealed record ModuleCell(string Text, ModuleTone? Tone);

/// <summary>A line on D47's side; d47 names the speaker and tags it with the module.</summary>
public sealed record ModuleMessage(string Text, DateTimeOffset? At) : ModulePart;
