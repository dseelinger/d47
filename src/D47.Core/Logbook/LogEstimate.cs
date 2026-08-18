using System.Globalization;
using D47.Core.Conversation;

namespace D47.Core.Logbook;

/// <summary>
/// What a log is about to cost, said before a byte is sent (list.md Phase 33, item 4).
/// <para>
/// <b>The item is about consent rather than about accounting.</b> "A Commander who set a monthly
/// cap and found it eaten by an unrequested essay about their Tuesday would be right to be angry"
/// — so the figure exists to be seen and agreed to, which is why producing one is a separate act
/// from spending it and why <see cref="LogbookBook.WriteAsync"/> refuses without one.
/// </para>
/// <para>
/// <b>Three outcomes and all three are sayable.</b> A figure; free, because the endpoint is on this
/// machine; or unpriced, because <see cref="PriceTable"/> has never heard of this model. The third
/// is the honest answer everywhere else in d47 and it stays honest here — a zero would be the
/// reassuring lie.
/// </para>
/// </summary>
public sealed record LogEstimate
{
    public required LogDigest Digest { get; init; }

    /// <summary>What the Commander chose.</summary>
    public required LogVoice Asked { get; init; }

    /// <summary>What will actually write, after the personality switch has had its say.</summary>
    public required LogVoice Used { get; init; }

    public required LogLength Length { get; init; }

    public required string ProviderId { get; init; }

    public required string Model { get; init; }

    public required int InputTokens { get; init; }

    public required int OutputTokens { get; init; }

    /// <summary>Null where the model has no published price. Zero where the endpoint is local.</summary>
    public decimal? Dollars { get; init; }

    /// <summary>The endpoint runs on this machine, so the answer is free rather than unknown.</summary>
    public bool Local { get; init; }

    public static LogEstimate For(
        LogDigest digest,
        PromptAssembly prompt,
        LogVoice asked,
        LogVoice used,
        LogLength length,
        string providerId,
        string model,
        ModelPrice? price,
        bool local)
    {
        var input = LogPrompt.Tokens(LogPrompt.Characters(prompt));
        var output = LogLengths.Tokens(length);

        // Priced as a cold turn, because it is one: nothing about this prompt was in a cache and
        // nothing about it will be read back. Quoting a cached rate would understate the one
        // request where being understated matters.
        var dollars = local
            ? 0m
            : price?.DollarsFor(new LlmUsage(input, output, 0, 0));

        return new LogEstimate
        {
            Digest = digest,
            Asked = asked,
            Used = used,
            Length = length,
            ProviderId = providerId,
            Model = model,
            InputTokens = input,
            OutputTokens = output,
            Dollars = dollars,
            Local = local,
        };
    }

    /// <summary>The price on its own, in the words the panel and the spoken answer both use.</summary>
    public string Price =>
        Local ? "nothing — that endpoint is on this machine"
        : Dollars is not { } dollars ? $"an amount I cannot work out; I have no published price for {Model}"
        : dollars < 0.01m ? "under a penny"
        : $"about {dollars.ToString("C2", CultureInfo.GetCultureInfo("en-US"))}";

    /// <summary>
    /// The whole quote. Shows its arithmetic, because a price nobody can check is a number to be
    /// taken on trust and this one is being asked to authorise a spend.
    /// </summary>
    public string Describe()
    {
        var text = new System.Text.StringBuilder();

        text.Append("A log of ").Append(Digest.Range.Label).Append(": ")
            .Append(Digest.Facts.Count.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" things I can account for, out of ")
            .Append(Digest.EventsRead.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" events in ")
            .Append(Digest.JournalsRead.ToString("N0", CultureInfo.InvariantCulture))
            .AppendLine(" journal file(s).");

        text.Append("Writing it would cost ").Append(Price).Append(" — about ")
            .Append(InputTokens.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" tokens in and ")
            .Append(LogPrompt.BudgetLine(Length))
            .Append(" back, through ")
            .Append(Model)
            .AppendLine(".");

        if (LogVoices.Degraded(Asked, Used) is { } degraded)
        {
            text.AppendLine(degraded);
        }

        if (Digest.FactsDropped > 0)
        {
            text.Append("That window has more in it than one log can hold; ")
                .Append(Digest.FactsDropped.ToString("N0", CultureInfo.InvariantCulture))
                .AppendLine(" further facts would be summarised rather than told.");
        }

        text.Append(Digest.Any
            ? "Say \"write the log\" and I will."
            : "There is nothing in that window to write about, so I would rather not spend anything on it.");

        return text.ToString();
    }
}
