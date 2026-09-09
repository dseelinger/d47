using D47.Core.Audio;

namespace D47.Core.Tests.Audio;

/// <summary>The real shipped set, with edits.</summary>
public sealed class EditedCueSource(Func<IEnumerable<string>, IEnumerable<string>> edit) : ICueSource
{
    private readonly EmbeddedCueSource _real = new(typeof(CueLibrary).Assembly);

    public IEnumerable<string> Names => edit(_real.Names);

    public Stream Open(string name) =>
        // A name the edit invented has no bytes behind it, so it borrows a real cue's.
        _real.Names.Contains(name) ? _real.Open(name) : _real.Open("D47.Core.Cues.idle");

    public static EditedCueSource Without(LoopState state) =>
        new(names => names.Where(name =>
            !name.Equals($"D47.Core.Cues.{state.ToString().ToLowerInvariant()}", StringComparison.OrdinalIgnoreCase)));

    public static EditedCueSource Plus(string cueName) =>
        new(names => names.Concat([$"D47.Core.Cues.{cueName}"]));
}
