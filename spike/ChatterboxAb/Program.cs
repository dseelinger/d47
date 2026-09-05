using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChatterboxAb;

/// <summary>
/// The blind A/B rig for Chatterbox Nano against Turbo, by one ear. Three commands: <c>prepare</c>
/// turns the sourced clips into five-to-seven-second voices and a candidate list; <c>serve</c>
/// hosts the review page (interesting or not) and the A/B page (A, B or same) on localhost and
/// records what is chosen; <c>synth</c> runs every approved voice through both models on the
/// probe and lays out the trials. Latency never enters it: every clip is made before it is heard.
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("""
                ChatterboxAb <command> <corpus dir> ...

                  prepare <corpus>                       manifests -> prepared clips + candidates.json
                  serve   <corpus> [port]                review at /, A/B at /ab (default 8765)
                  synth   <corpus> <probe.exe> [lines]   approved voices x lines x {nano,turbo} -> trials
                """);
            return 2;
        }

        var corpus = Path.GetFullPath(args[1]);

        return args[0] switch
        {
            "prepare" => Prepare(corpus),
            "serve" => Serve(corpus, args.Length > 2 ? int.Parse(args[2]) : 8765),
            "synth" => Synth(corpus, args[2], args.Length > 3 ? args[3] : Path.Combine(AppContext.BaseDirectory, "web", "lines.json")),
            _ => 2,
        };
    }

    // ------------------------------------------------------------------ prepare

    private static int Prepare(string corpus)
    {
        // A voice that lost a file to the sweep is cut again from what survived, and the verdict
        // recorded against it goes: it was cast on audio that is no longer what plays. Doing this
        // here rather than by hand is the point — the sweep found nineteen copies of one
        // placeholder, and the two voices whose stale clips it did not re-cut were the two that
        // reached the ear anyway.
        foreach (var id in Stubs(corpus))
        {
            var prepared = Path.Combine(corpus, "prepared", id.Replace('/', Path.DirectorySeparatorChar) + ".wav");

            if (File.Exists(prepared))
            {
                File.Delete(prepared);
                Console.WriteLine($"{id}: lost a file, will be cut again");
            }

            var verdicts = Load(Path.Combine(corpus, "verdicts.json"))?.AsObject();

            if (verdicts?.Remove(id) == true)
            {
                Save(Path.Combine(corpus, "verdicts.json"), verdicts);
                Console.WriteLine($"{id}: verdict cleared, it needs another listen");
            }
        }

        var candidates = new JsonArray();
        var existing = Load(Path.Combine(corpus, "candidates.json"))?.AsArray() ?? [];

        // A candidate whose clips have all gone — a stub sweep, or a file deleted by hand — is
        // dropped rather than left pointing at audio that is no longer there.
        foreach (var stale in existing.Where(c =>
            !File.Exists(Path.Combine(corpus, c!["file"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar)))).ToList())
        {
            Console.WriteLine($"{stale!["voice"]!.GetValue<string>()}: prepared clip is gone, dropped");
            existing.Remove(stale);
        }

        var known = existing.Select(c => c!["id"]!.GetValue<string>()).ToHashSet();

        foreach (var c in existing)
        {
            candidates.Add(c!.DeepClone());
        }

        var manifests = Directory.Exists(Path.Combine(corpus, "manifests"))
            ? Directory.GetFiles(Path.Combine(corpus, "manifests"), "*.json")
            : [];

        foreach (var manifest in manifests)
        {
            JsonArray entries;

            try
            {
                entries = Load(manifest)!.AsArray();
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                Console.WriteLine($"skipping {Path.GetFileName(manifest)}: {exception.Message}");
                continue;
            }

            foreach (var entry in entries)
            {
                var voice = entry!["voice"]!.GetValue<string>();
                var category = entry["category"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(manifest);

                // Agents write a path either from the corpus root or from raw/, and one wrote an
                // absolute one. Take whichever exists rather than insisting on a convention that
                // was only ever in the brief.
                var files = entry["files"]!.AsArray()
                    .Select(f => f!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar))
                    .Select(f => new[] { Path.Combine(corpus, "raw", f), Path.Combine(corpus, f), f }
                        .FirstOrDefault(File.Exists))
                    .OfType<string>()
                    .ToList();

                if (files.Count == 0)
                {
                    Console.WriteLine($"no files on disk for {voice}");
                    continue;
                }

                // Keyed by the files' own prefix, not the voice's name, so a voice first seen by
                // scanning the folder (below) keeps its id and its verdict when its manifest lands.
                var id = $"{category}/{Prefix(files[0])}";

                if (known.Contains(id))
                {
                    // Already cut, from the folder scan of an earlier run. The manifest is still
                    // worth reading for what the scan could not know — the voice's real name, its
                    // licence and where it came from — and none of that changes the audio or the
                    // verdict already recorded against this id.
                    var already = candidates.FirstOrDefault(c => c!["id"]!.GetValue<string>() == id);

                    if (already is not null)
                    {
                        already["voice"] = voice;
                        already["licence"] = entry["licence"]?.GetValue<string>() ?? "unknown";
                        already["sources"] = new JsonArray(Sources(entry).Select(s => (JsonNode)s).ToArray());
                    }

                    continue;
                }

                Add(candidates, id, voice, category, files, entry["where"]?.GetValue<string>(),
                    entry["licence"]?.GetValue<string>() ?? "unknown", Sources(entry), corpus);
                known.Add(id);
            }
        }

        // Voices whose manifest has not landed yet, from the files alone: the agents name them
        // <voice>-<n>.<ext>, so the prefix is the voice. The manifest, when it comes, keeps the id
        // and only improves the label.
        var raw = Path.Combine(corpus, "raw");

        foreach (var folder in Directory.Exists(raw) ? Directory.GetDirectories(raw) : [])
        {
            var category = Path.GetFileName(folder);

            foreach (var group in Directory.GetFiles(folder).GroupBy(Prefix).OrderBy(g => g.Key))
            {
                var id = $"{category}/{group.Key}";

                if (known.Contains(id))
                {
                    continue;
                }

                var files = group.OrderBy(f => Number(f)).ToList();
                Add(candidates, id, group.Key.Replace('-', ' '), category, files, null, "unverified, listening test only", [], corpus);
                known.Add(id);
            }
        }

        Mundane(candidates, known, corpus);

        Save(Path.Combine(corpus, "candidates.json"), candidates);
        Console.WriteLine($"{candidates.Count} candidates -> candidates.json");
        return 0;
    }

    /// <summary>
    /// Deletes the clips that are not clips. A clip site whose signed link has expired serves a
    /// spoken placeholder — "please refresh the page to hear the sounds" — with a 200 and an audio
    /// content type, so nothing upstream can tell it from a voice. Two of them reached this corpus
    /// from two different sites, one of them 19 times, and they only showed up when a person played
    /// them. They are byte-identical wherever they land, which is what makes them findable: any
    /// small file whose exact bytes repeat across unrelated voices is a placeholder, because two
    /// real recordings of two different actors never collide.
    /// </summary>
    private static HashSet<string> Stubs(string corpus)
    {
        var hit = new HashSet<string>();
        var raw = Path.Combine(corpus, "raw");

        if (!Directory.Exists(raw))
        {
            return hit;
        }

        var byHash = new Dictionary<string, List<string>>();

        foreach (var file in Directory.GetFiles(raw, "*", SearchOption.AllDirectories))
        {
            // Only small files: a placeholder is a few seconds, and hashing a 40 MB interview to
            // discover it is unique is time spent for nothing.
            if (new FileInfo(file).Length > 400 * 1024)
            {
                continue;
            }

            var hash = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(File.ReadAllBytes(file)));

            if (!byHash.TryGetValue(hash, out var same))
            {
                byHash[hash] = same = [];
            }

            same.Add(file);
        }

        foreach (var (_, files) in byHash)
        {
            // Two copies under one voice are an agent fetching the same URL twice, which is
            // harmless. The same bytes under two voices are a placeholder.
            if (files.Count < 2 || files.Select(f => $"{Path.GetFileName(Path.GetDirectoryName(f))}/{Prefix(f)}").Distinct().Count() < 2)
            {
                continue;
            }

            Console.WriteLine($"placeholder in {files.Count} files, deleting: {string.Join(", ", files.Select(Path.GetFileName).Take(4))}…");

            foreach (var file in files)
            {
                hit.Add($"{Path.GetFileName(Path.GetDirectoryName(file))}/{Prefix(file)}");
                File.Delete(file);
            }
        }

        return hit;
    }

    private static void Add(
        JsonArray candidates, string id, string voice, string category, IReadOnlyList<string> files,
        string? where, string licence, string[] sources, string corpus)
    {
        var target = Path.Combine(corpus, "prepared", id.Replace('/', Path.DirectorySeparatorChar) + ".wav");
        double seconds;

        try
        {
            var samples = Clip.Prepare(files, where);
            seconds = Clip.Seconds(samples);

            if (seconds < 3)
            {
                Console.WriteLine($"{voice}: only {seconds:F1}s of speech, skipped");
                return;
            }

            Clip.WriteWav(target, samples);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"{voice}: {exception.Message}");
            return;
        }

        candidates.Add(new JsonObject
        {
            ["id"] = id,
            ["voice"] = voice,
            ["category"] = category,
            ["file"] = "prepared/" + id + ".wav",
            ["seconds"] = Math.Round(seconds, 2),
            ["licence"] = licence,
            ["sources"] = new JsonArray(sources.Select(s => (JsonNode)s).ToArray()),
        });

        Console.WriteLine($"{voice,-48} {seconds,5:F1}s");
    }

    /// <summary>The control group: ordinary readers, and actors doing the same line in seven moods.</summary>
    private static void Mundane(JsonArray candidates, HashSet<string> known, string corpus)
    {
        var libri = Path.Combine(corpus, "mundane", "LibriSpeech", "dev-clean");

        if (Directory.Exists(libri))
        {
            foreach (var speaker in Directory.GetDirectories(libri).OrderBy(d => d).Take(12))
            {
                var name = Path.GetFileName(speaker);
                var id = $"mundane/libri-{name}";

                if (known.Contains(id))
                {
                    continue;
                }

                var files = Directory.GetFiles(speaker, "*.flac", SearchOption.AllDirectories).OrderBy(f => f).Take(4).ToList();
                Add(candidates, id, $"LibriSpeech reader {name}", "mundane", files, null, "CC BY 4.0", ["https://www.openslr.org/12"], corpus);
                known.Add(id);
            }
        }

        var ravdess = Path.Combine(corpus, "mundane", "ravdess");

        if (Directory.Exists(ravdess))
        {
            var moods = new Dictionary<string, string> { ["03"] = "happy", ["04"] = "sad", ["05"] = "angry", ["06"] = "fearful", ["07"] = "disgusted", ["08"] = "surprised" };

            foreach (var actor in Directory.GetDirectories(ravdess, "Actor_*").OrderBy(d => d).Take(6))
            {
                var number = Path.GetFileName(actor)[6..];

                foreach (var (code, mood) in moods)
                {
                    var id = $"mundane/ravdess-{number}-{mood}";

                    if (known.Contains(id))
                    {
                        continue;
                    }

                    // 03-01-<mood>-02 is speech, strong intensity; both statements, both repeats.
                    var files = Directory.GetFiles(actor, $"03-01-{code}-02-*.wav").OrderBy(f => f).ToList();
                    Add(candidates, id, $"RAVDESS actor {number}, {mood}", "mundane", files, null, "CC BY-NC-SA 4.0, listening test only", ["https://zenodo.org/records/1188976"], corpus);
                    known.Add(id);
                }
            }
        }
    }

    // ------------------------------------------------------------------ serve

    private static int Serve(string corpus, int port)
    {
        var web = Path.Combine(AppContext.BaseDirectory, "web");
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        Console.WriteLine($"review  http://localhost:{port}/");
        Console.WriteLine($"a/b     http://localhost:{port}/ab");
        Console.WriteLine("ctrl+c stops");

        while (true)
        {
            var context = listener.GetContext();

            // One request per pool thread: a browser fetching six audio files at once must not
            // queue behind the first, and a client that drops mid-transfer must not take the
            // server with it.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Handle(context, corpus, web);
                }
                catch (Exception exception)
                {
                    try
                    {
                        Respond(context, 500, "text/plain", Encoding.UTF8.GetBytes(exception.ToString()));
                    }
                    catch (Exception)
                    {
                        // The response was already under way when it failed; nothing to say into it.
                        context.Response.Abort();
                    }
                }
            });
        }
    }

    private static void Handle(HttpListenerContext context, string corpus, string web)
    {
        var path = context.Request.Url!.AbsolutePath;
        var method = context.Request.HttpMethod;

        switch (path)
        {
            case "/":
                Static(context, Path.Combine(web, "review.html"));
                return;
            case "/ab":
                Static(context, Path.Combine(web, "ab.html"));
                return;
            case "/api/candidates" when method == "GET":
            {
                var candidates = Load(Path.Combine(corpus, "candidates.json"))?.AsArray() ?? [];
                var verdicts = Load(Path.Combine(corpus, "verdicts.json"))?.AsObject() ?? [];

                foreach (var c in candidates)
                {
                    c!["verdict"] = verdicts[c["id"]!.GetValue<string>()]?.DeepClone();
                }

                Respond(context, 200, "application/json", Bytes(candidates));
                return;
            }
            case "/api/verdict" when method == "POST":
            {
                var body = Body(context);
                var verdicts = Load(Path.Combine(corpus, "verdicts.json"))?.AsObject() ?? [];
                verdicts[body["id"]!.GetValue<string>()] = body["verdict"]!.GetValue<string>();
                Save(Path.Combine(corpus, "verdicts.json"), verdicts);
                Respond(context, 200, "application/json", Bytes(new JsonObject { ["approved"] = verdicts.Count(v => v.Value?.GetValue<string>() == "yes") }));
                return;
            }
            case "/api/trials" when method == "GET":
                Respond(context, 200, "application/json", Bytes(TrialState(corpus)));
                return;
            case "/api/pick" when method == "POST":
            {
                var body = Body(context);
                var picks = Load(Path.Combine(corpus, "picks.json"))?.AsArray() ?? [];
                picks.Add(new JsonObject
                {
                    ["trial"] = body["trial"]!.GetValue<string>(),
                    ["pick"] = body["pick"]!.GetValue<string>(),
                    ["at"] = DateTime.UtcNow.ToString("o"),
                });
                Save(Path.Combine(corpus, "picks.json"), picks);
                Respond(context, 200, "application/json", Bytes(TrialState(corpus)));
                return;
            }
        }

        if (path.StartsWith("/corpus/"))
        {
            var relative = Uri.UnescapeDataString(path["/corpus/".Length..]).Replace('/', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(corpus, relative));

            if (full.StartsWith(corpus) && File.Exists(full))
            {
                Static(context, full);
                return;
            }
        }

        if (path.StartsWith("/web/"))
        {
            var full = Path.GetFullPath(Path.Combine(web, path["/web/".Length..]));

            if (full.StartsWith(web) && File.Exists(full))
            {
                Static(context, full);
                return;
            }
        }

        Respond(context, 404, "text/plain", Encoding.UTF8.GetBytes("no"));
    }

    /// <summary>Trials, picks, and the running verdict, in one object the page polls.</summary>
    private static JsonObject TrialState(string corpus)
    {
        var trials = Load(Path.Combine(corpus, "trials.json"))?.AsArray() ?? [];
        var picks = Load(Path.Combine(corpus, "picks.json"))?.AsArray() ?? [];
        var byTrial = trials.ToDictionary(t => t!["id"]!.GetValue<string>(), t => t!);

        int turbo = 0, nano = 0, ties = 0;

        foreach (var pick in picks)
        {
            if (!byTrial.TryGetValue(pick!["trial"]!.GetValue<string>(), out var trial))
            {
                continue;
            }

            var choice = pick["pick"]!.GetValue<string>();

            if (choice == "tie")
            {
                ties++;
                continue;
            }

            var model = trial[choice + "Model"]!.GetValue<string>();

            if (model == "turbo")
            {
                turbo++;
            }
            else
            {
                nano++;
            }
        }

        var decisive = turbo + nano;
        var p = Stats.BinomialP(turbo, decisive);
        var (low, high) = Stats.Wilson(turbo, decisive);
        var total = picks.Count;
        var done = (decisive >= Stats.MinDecisive && p < Stats.Alpha) || total >= Stats.MaxTrials;
        var answered = picks.Select(k => k!["trial"]!.GetValue<string>()).ToHashSet();

        return new JsonObject
        {
            ["trials"] = trials.DeepClone(),
            ["answered"] = new JsonArray(answered.Select(a => (JsonNode)a).ToArray()),
            ["turbo"] = turbo,
            ["nano"] = nano,
            ["ties"] = ties,
            ["decisive"] = decisive,
            ["p"] = Math.Round(p, 4),
            ["low"] = Math.Round(low, 3),
            ["high"] = Math.Round(high, 3),
            ["done"] = done,
            ["minDecisive"] = Stats.MinDecisive,
            ["maxTrials"] = Stats.MaxTrials,
        };
    }

    // ------------------------------------------------------------------ synth

    private static int Synth(string corpus, string probe, string linesFile)
    {
        var candidates = Load(Path.Combine(corpus, "candidates.json"))?.AsArray() ?? [];
        var verdicts = Load(Path.Combine(corpus, "verdicts.json"))?.AsObject() ?? [];
        var lines = Load(linesFile)!.AsArray();
        var approved = candidates.Where(c => verdicts[c!["id"]!.GetValue<string>()]?.GetValue<string>() == "yes").ToList();
        var trials = Load(Path.Combine(corpus, "trials.json"))?.AsArray() ?? [];
        var have = trials.Select(t => t!["id"]!.GetValue<string>()).ToHashSet();
        var random = new Random(47);
        var made = 0;

        Console.WriteLine($"{approved.Count} approved voices x {lines.Count} lines x 2 models");

        foreach (var candidate in approved)
        {
            var id = candidate!["id"]!.GetValue<string>();
            var reference = Path.Combine(corpus, candidate["file"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar));

            foreach (var line in lines)
            {
                var lineId = line!["id"]!.GetValue<string>();
                var text = line["text"]!.GetValue<string>();
                var trialId = $"{id}|{lineId}";
                var outputs = new Dictionary<string, string>();
                var ok = true;

                foreach (var model in new[] { "nano", "turbo" })
                {
                    var output = Path.Combine(corpus, "synth", $"{Slug(id)}-{lineId}-{model}.wav");
                    outputs[model] = "synth/" + Path.GetFileName(output);

                    if (File.Exists(output))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);

                    var start = new ProcessStartInfo(probe)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    };

                    foreach (var a in new[] { "say", text, reference, output, "--variant", model, "--dtype", "q4f16", "--threads", "8", "--decoder-threads", "16" })
                    {
                        start.ArgumentList.Add(a);
                    }

                    using var process = Process.Start(start)!;
                    var stdout = process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0 || !File.Exists(output))
                    {
                        Console.WriteLine($"FAILED {id} {lineId} {model}: {stdout.Split('\n').LastOrDefault(l => l.Length > 0)}");
                        ok = false;
                        break;
                    }

                    made++;
                    Console.WriteLine($"{id,-40} {lineId,-10} {model,-6} {stdout.Split('\n').FirstOrDefault(l => l.Contains("first sound"))?.Trim()}");
                }

                if (!ok || have.Contains(trialId))
                {
                    continue;
                }

                var turboFirst = random.Next(2) == 0;

                trials.Add(new JsonObject
                {
                    ["id"] = trialId,
                    ["voice"] = candidate["voice"]!.GetValue<string>(),
                    ["line"] = lineId,
                    ["a"] = outputs[turboFirst ? "turbo" : "nano"],
                    ["b"] = outputs[turboFirst ? "nano" : "turbo"],
                    ["aModel"] = turboFirst ? "turbo" : "nano",
                    ["bModel"] = turboFirst ? "nano" : "turbo",
                });
                have.Add(trialId);
            }
        }

        // Shuffled once, so the page walks them in an order that hides nothing.
        var shuffled = new JsonArray(trials.Select(t => t!.DeepClone()).OrderBy(_ => random.Next()).ToArray());
        Save(Path.Combine(corpus, "trials.json"), shuffled);
        Console.WriteLine($"{made} clips made, {shuffled.Count} trials -> trials.json");
        return 0;
    }

    // ------------------------------------------------------------------ plumbing

    private static string Slug(string text) =>
        new string(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    /// <summary>
    /// The URLs a manifest entry cites. Some agents wrote a bare string per source and one wrote an
    /// object per file, so take the object's own url and fall back to its text.
    /// </summary>
    private static string[] Sources(JsonNode entry) =>
        [.. (entry["sources"]?.AsArray() ?? []).Select(s => s switch
        {
            JsonValue value => value.ToString(),
            JsonObject o => o["url"]?.ToString() ?? o["item"]?.ToString() ?? o.ToJsonString(),
            _ => s?.ToJsonString() ?? string.Empty,
        })];

    /// <summary>"vader-3.mp3" -> "vader"; a file with no number keeps its whole stem.</summary>
    private static string Prefix(string file)
    {
        var stem = Path.GetFileNameWithoutExtension(file);
        var dash = stem.LastIndexOf('-');

        return dash > 0 && int.TryParse(stem[(dash + 1)..], out _) ? stem[..dash] : stem;
    }

    private static int Number(string file)
    {
        var stem = Path.GetFileNameWithoutExtension(file);
        var dash = stem.LastIndexOf('-');

        return dash > 0 && int.TryParse(stem[(dash + 1)..], out var n) ? n : 0;
    }

    private static JsonNode? Load(string path) =>
        File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null;

    private static void Save(string path, JsonNode node) =>
        File.WriteAllText(path, node.ToJsonString(Json));

    private static byte[] Bytes(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());

    private static JsonObject Body(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
    }

    private static void Static(HttpListenerContext context, string file)
    {
        var type = Path.GetExtension(file).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".json" => "application/json",
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".flac" => "audio/flac",
            _ => "application/octet-stream",
        };

        Respond(context, 200, type, File.ReadAllBytes(file));
    }

    private static void Respond(HttpListenerContext context, int status, string type, byte[] body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = type;
        context.Response.ContentLength64 = body.Length;

        // A HEAD gets the headers and no body — writing one is what the listener refuses.
        if (context.Request.HttpMethod != "HEAD")
        {
            context.Response.OutputStream.Write(body);
        }

        context.Response.Close();
    }
}
