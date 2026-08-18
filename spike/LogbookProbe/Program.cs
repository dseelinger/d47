using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Logbook;
using D47.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogbookProbe;

/// <summary>
/// The acceptance run for list.md Phase 33: "a log generated from a corpus session read against
/// that session's events by hand, once, deliberately".
/// <para>
/// Two modes, and the default one spends nothing. Without <c>--send</c> it resolves the window,
/// builds the digest, prints every fact with its supporting events, and prints the estimate — the
/// whole of the pipeline except the HTTP call. With <c>--send</c> it makes the one request, using
/// the key already stored in the installed build's <c>secrets.json</c>.
/// </para>
/// <para>
/// A spike rather than a test: it reads a real corpus that only exists on one machine, and it can
/// spend real money. Neither belongs in CI.
/// </para>
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var send = args.Contains("--send", StringComparer.Ordinal);
        var journals = Argument(args, "--journals")
            ?? D47.Core.Journal.JournalFolder.DefaultPath();
        var data = Argument(args, "--data")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "d47", "data");
        var output = Argument(args, "--out") ?? Path.Combine(Path.GetTempPath(), "d47-logbook-probe");
        var span = LogRanges.Parse(Argument(args, "--span") ?? "session");
        var voice = LogVoices.Parse(Argument(args, "--voice") ?? "first-person");
        var length = LogLengths.Parse(Argument(args, "--length") ?? "standard");

        if (!Directory.Exists(journals))
        {
            Console.Error.WriteLine($"No journal folder at {journals}");
            return 2;
        }

        var files = Directory
            .EnumerateFiles(journals, D47.Core.Journal.JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"{files.Count} journals in {journals}");

        var now = DateTimeOffset.Now;
        var logger = NullLogger.Instance;
        var range = LogRanges.Resolve(span, now, files, logger);

        Console.WriteLine($"Window: {range.Label} — {range.From:u} to {range.To:u}");
        Console.WriteLine($"Files in the window: {LogRanges.FilesFor(files, range).Count}");
        Console.WriteLine();

        var digest = new LogDigestBuilder(NullLogger<LogDigestBuilder>.Instance).Build(files, range);

        Console.WriteLine("=== THE DIGEST, EXACTLY AS THE MODEL WOULD SEE IT ===");
        Console.WriteLine();
        Console.WriteLine(digest.Render());

        Console.WriteLine("=== THE EVENTS BEHIND EACH FACT ===");
        Console.WriteLine();

        foreach (var fact in digest.Facts)
        {
            Console.WriteLine($"[{fact.Id}] {fact.Statement}");
            Console.WriteLine($"     {fact.Provenance()}");
        }

        Console.WriteLine();

        var model = Argument(args, "--model") ?? "claude-opus-5";
        var key = ApiKey(data);

        if (key is null)
        {
            Console.Error.WriteLine($"No anthropic.apiKey in {data}\\secrets.json");
            return 3;
        }

        var provider = new AnthropicLlmProvider(key);

        var context = new LogbookContext
        {
            Provider = provider,
            Model = model,
            PersonalityEnabled = LogVoices.NeedsPersona(voice),
            Persona = LogVoices.NeedsPersona(voice)
                ? D47.Core.Persona.PersonaCatalog.Resolve(null).RenderBlock(null)
                : null,
            AboutMe = AboutMe(data),
            Version = "probe",
        };

        var book = new LogbookBook(
            new LogFolder(output, NullLogger<LogFolder>.Instance),
            new LogDigestBuilder(NullLogger<LogDigestBuilder>.Instance),
            new LogWriter(NullLogger<LogWriter>.Instance),
            () => new LogbookSettings
            {
                Voice = LogVoices.IdOf(voice),
                Range = LogRanges.IdOf(span),
                Length = LogLengths.IdOf(length),
            },
            () => files,
            () => now,
            () => context,
            NullLogger<LogbookBook>.Instance);

        book.Estimate(null, null, null, out var quote);

        Console.WriteLine("=== THE ESTIMATE ===");
        Console.WriteLine();
        Console.WriteLine(quote);
        Console.WriteLine();

        if (!send)
        {
            Console.WriteLine("Nothing was sent. Pass --send to spend the figure above.");
            return 0;
        }

        Console.WriteLine("Sending…");

        var outcome = await book.WriteAsync(CancellationToken.None);

        Console.WriteLine(outcome.Message);

        if (outcome.Written?.Path is { } path)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {path} ===");
            Console.WriteLine();
            Console.WriteLine(File.ReadAllText(path));
        }

        return outcome.Ok ? 0 : 1;
    }

    private static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>
    /// The key out of the installed build's own store. DPAPI, CurrentUser scope, no entropy —
    /// which is what <see cref="SecretStore"/> writes, and only this user can read it back.
    /// </summary>
    private static string? ApiKey(string data)
    {
        var path = Path.Combine(data, "secrets.json");

        if (!File.Exists(path))
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));

        if (document?.GetValueOrDefault("anthropic.apiKey") is not { Length: > 0 } blob)
        {
            return null;
        }

        return Encoding.UTF8.GetString(
            ProtectedData.Unprotect(Convert.FromBase64String(blob), null, DataProtectionScope.CurrentUser));
    }

    private static string? AboutMe(string data)
    {
        var path = Path.Combine(data, "settings.json");

        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.TryGetProperty("llm", out var llm)
            && llm.TryGetProperty("aboutMe", out var about)
            && about.ValueKind == JsonValueKind.String
                ? about.GetString()
                : null;
    }
}
