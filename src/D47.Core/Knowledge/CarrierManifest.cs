using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>
/// One commodity the Commander has told d47 is on their carrier, and when they said so.
/// </summary>
/// <param name="Commodity">
/// The market's spelling, which is the join everywhere in this phase: the depot writes
/// <c>Name_Localised</c> on every row, both market sources are keyed by the same display name, and
/// so is this. The symbol is what joins the depot to the <em>hold</em> and is not this store's
/// business.
/// </param>
/// <param name="Tonnes">How many.</param>
/// <param name="SaidAt">
/// When they said it. <b>Shown wherever the figure is used</b>, because this is the one number in a
/// sourcing plan that d47 has no way of checking: a week-old "300 tritium" is a week-old memory of
/// a carrier that has been flown since.
/// </param>
public sealed record CarrierStock(string Commodity, int Tonnes, DateTimeOffset SaidAt);

/// <summary>
/// What the Commander says is on their fleet carrier (list.md Phase 50, amended by the Commander
/// on 2026-08-25).
/// <para>
/// <b>Told, never derived, and the difference is the whole reason this class exists.</b> The plan
/// of record has the carrier out of the arithmetic entirely, and the measurement behind that ruling
/// stands: accumulating <c>CargoTransfer</c> against <c>CarrierStats</c> came out <b>wrong 679
/// times against right 347</b> and drove eleven commodities negative, so a plan that subtracted a
/// derived figure would send a Commander to buy 200 tonnes they already have or — far worse — not
/// to buy 200 they do not. <see cref="CarrierState.CargoTonnes"/> is a tonnage and nothing more for
/// exactly that reason.
/// </para>
/// <para>
/// <b>The Commander's own figure is a different kind of thing.</b> They can see their carrier's
/// inventory screen; d47 cannot. So the route the plan left open — <i>"a Commander-entered figure,
/// not a derived one"</i> — is the route taken here, and it is subtracted because it is a
/// statement of fact rather than an inference from events. What d47 owes in return is to say whose
/// number it is and how old, on every answer that used it, so a stale figure reads as a stale
/// figure rather than as arithmetic.
/// </para>
/// <para>
/// <b>Per Commander, keyed in the document rather than in a filename</b>, because the id comes out
/// of the journal — the same arrangement <c>settings.json</c> makes for the rows that are a
/// person's rather than an installation's.
/// </para>
/// </summary>
public sealed class CarrierManifest
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>The key a Commander with no journal read yet is filed under.</summary>
    private const string Nobody = "";

    private readonly Lock _gate = new();
    private readonly string _path;
    private readonly ILogger _log;

    private Document _document;

    public CarrierManifest(string path, ILogger<CarrierManifest> log)
    {
        _path = path;
        _log = log;
        _document = Read();
    }

    /// <summary>Raised when a figure is set or cleared, so a surface can redraw without polling.</summary>
    public event Action? Changed;

    /// <summary>What this Commander says is aboard, newest statement first.</summary>
    public IReadOnlyList<CarrierStock> For(string? commanderFid)
    {
        lock (_gate)
        {
            return _document.Commanders.TryGetValue(commanderFid ?? Nobody, out var held)
                ? [.. held.Select(pair => new CarrierStock(pair.Key, pair.Value.Tonnes, pair.Value.At))
                    .OrderByDescending(stock => stock.SaidAt)]
                : [];
        }
    }

    /// <summary>
    /// Records a figure. <b>Zero or less removes it</b> rather than storing a nought: "I have none"
    /// and "I have not said" are the same instruction to a plan, and keeping a zero row would leave
    /// the page listing commodities the Commander has finished with.
    /// </summary>
    public void Set(string? commanderFid, string commodity, int tonnes, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(commodity))
        {
            return;
        }

        lock (_gate)
        {
            var key = commanderFid ?? Nobody;

            if (!_document.Commanders.TryGetValue(key, out var held))
            {
                held = new Dictionary<string, Held>(StringComparer.OrdinalIgnoreCase);
                _document.Commanders[key] = held;
            }

            if (tonnes <= 0)
            {
                held.Remove(commodity.Trim());
            }
            else
            {
                held[commodity.Trim()] = new Held { Tonnes = tonnes, At = at };
            }

            Write();
        }

        Changed?.Invoke();
    }

    /// <summary>Forgets everything this Commander has said, for a carrier that has been emptied.</summary>
    public void Clear(string? commanderFid)
    {
        lock (_gate)
        {
            if (!_document.Commanders.Remove(commanderFid ?? Nobody))
            {
                return;
            }

            Write();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// The outstanding list with what the Commander says is already on the carrier taken off it,
    /// and what was taken off.
    /// <para>
    /// <b>A row cleared entirely drops out</b>, because there is nothing left to go and buy — but it
    /// is still named in <c>counted</c>, so the answer can say why a commodity a Commander expected
    /// to see is not on the shopping list. A sourcing plan that silently lost a row is the failure
    /// this phase's acceptance test exists to prevent, and it does not become acceptable because
    /// the reason was a good one.
    /// </para>
    /// </summary>
    public static (IReadOnlyList<ConstructionResource> Outstanding, IReadOnlyList<CarrierStock> Counted) Deduct(
        IReadOnlyList<ConstructionResource> outstanding,
        IReadOnlyList<CarrierStock> aboard)
    {
        if (aboard.Count == 0)
        {
            return (outstanding, []);
        }

        var held = aboard
            .GroupBy(stock => stock.Commodity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var left = new List<ConstructionResource>(outstanding.Count);
        var counted = new List<CarrierStock>();

        foreach (var resource in outstanding)
        {
            if (!held.TryGetValue(resource.Name, out var stock) || stock.Tonnes <= 0)
            {
                left.Add(resource);
                continue;
            }

            // Never more than the site wants. Five hundred tonnes on the carrier against sixty
            // outstanding is sixty tonnes of progress and four hundred and forty tonnes of
            // something else to do with, and a plan that counted all five hundred would be
            // subtracting a number that is not about this site.
            var used = Math.Min(stock.Tonnes, resource.Remaining);

            counted.Add(stock with { Tonnes = used });

            if (used < resource.Remaining)
            {
                left.Add(resource with { Provided = resource.Provided + used });
            }
        }

        return (left, counted);
    }

    private Document Read()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new Document();
            }

            return JsonSerializer.Deserialize<Document>(File.ReadAllText(_path), Json) ?? new Document();
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            // A carrier figure is a convenience and losing one costs a Commander a sentence of
            // typing. Refusing to start over it would cost them the app.
            _log.LogWarning(error, "The carrier manifest at {Path} could not be read; starting empty.", _path);

            return new Document();
        }
    }

    private void Write()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_document, Json));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning(error, "The carrier manifest at {Path} could not be written.", _path);
        }
    }

    private sealed class Document
    {
        public Dictionary<string, Dictionary<string, Held>> Commanders { get; set; } =
            new(StringComparer.Ordinal);
    }

    private sealed class Held
    {
        public int Tonnes { get; set; }

        public DateTimeOffset At { get; set; }
    }
}
