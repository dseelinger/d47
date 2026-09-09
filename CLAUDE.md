# CLAUDE.md

> **Generated, not written by the maintainer.** Rebuilt from evidence in the tree after the
> original was removed. Check anything here against the code before relying on it.

## What d47 is

Directive 47 — a voice companion for Elite Dangerous on Windows 11. Reads the journal Elite
already writes, answers out loud, renders one panel to a desktop window and a SteamVR overlay.
MIT code; Frontier game data under their media-usage rules (`LICENSE`, `NOTICE`). Product docs:
`docs/index.md`.

## Build and test

```
dotnet build d47.slnx -c Debug      # clean: 0 warnings, 0 errors
dotnet test  d47.slnx -c Debug      # the whole suite; a release gate, not a working loop

dotnet test tests/D47.Core.Tests --filter FullyQualifiedName~Ticking   # the working loop
```

- SDK pinned by `global.json` to `10.0.400`, `rollForward: disable` — exact, because the suite
  now runs partly here and partly on the runner and the two must agree on a toolchain.
- `Directory.Build.props` sets `net10.0-windows`, `Nullable`, `ImplicitUsings`,
  `EnforceCodeStyleInBuild` and **`TreatWarningsAsErrors`**. A warning is a build break. There is
  no `#pragma warning disable` or `SuppressMessage` in `src/`; adding the first one is a
  deliberate act.
- `D47.App` alone targets `net10.0-windows10.0.26100.0`, for `Windows.Gaming.Input`. Publishes
  single-file, self-contained, `win-x64`.
- xunit v3. `D47.Core.Tests` also references `Microsoft.CodeAnalysis.CSharp`: one gate reads
  source with the compiler because grep under-reported it (#270).

`src/D47.Core/D47.Core.csproj:75` embeds `..\..\CHANGELOG.md` as `D47.Core.Changelog`, read by
the About dialog. The changelog is a changelog: nothing in the release chain parses it.

## The layering rule

- **`D47.Core` depends on nothing** but `Microsoft.Extensions.Logging.Abstractions` and
  `System.Security.Cryptography.ProtectedData`. No UI, no audio hardware, no vendor SDK.
- **Each spoke references only Core** — `D47.Audio`, `D47.Knowledge`, `D47.Llm`, `D47.Stt`,
  `D47.Tts`, `D47.Vr`. No spoke references another spoke.
- **`D47.App` references all seven** and is the only place they meet.

Core declares the seams (`ILlmProvider`, `ITtsProvider`, `ISpeechTranscriber`, `IAudioSink`,
`IGalaxyService`, `IGameInput`); each spoke implements one.

Enforced by `tests/D47.Core.Tests/CoreDependencyTests.cs`, which reflects over referenced
assembly names and fails on `Avalonia`, `Vortice`, `NAudio`, `Whisper`, `Anthropic`, `OpenAI` or
`Serilog.Sinks`. The rule is what makes the headless replay harness possible: break it and game
logic can no longer run against journal fixtures with no game, device or network.

## The tick discipline

`src/D47.Core/Ticking/TickLoop.cs` owns no thread and reads no clock — the caller supplies the
time. `src/D47.App/Ticking/TickDriver.cs` drives it at 100 ms on a dedicated background thread,
not the pool. `JournalReader` follows the same rule.

1. **A tick is synchronous and must not block.** All subscribers share one thread; one that waits
   on the network stalls push-to-talk and every callout behind it. Awaitable work goes to the
   thread pool through a queue.
2. **Registration order is load-bearing.** The journal is polled before anything reading game
   state.
3. **A throwing subscriber is caught, logged and skipped**, reporting throttled after the first.

## Trust levels

`Capabilities.ToolCaller` distinguishes `Commander` from `Model`. A protected tool is reachable
by the keyword router, the panel and a hotkey, and refused to the LLM. Trust is a property of the
caller, not the modality. `TurnLoop` tries `MatchSetting`, then
`MatchToolCommand`, then `Match` — all model-free — and reaches the LLM only if none match.

## Two properties worth knowing

**Egress is disclosed, not promised.** `Configuration/EgressDisclosure.cs` states what each setting
sends and where, computed from live settings; `get_data_egress` and the **Privacy and egress**
panel section answer from it rather than from documentation. A new destination means a new
disclosure entry.

**No elevation.** `installer/d47.iss` uses `PrivilegesRequired=lowest` and installs per-user to a
fixed unversioned `%LOCALAPPDATA%\Programs\d47`, because `data\` lives beside the exe.

Secrets are DPAPI-protected (`Configuration/SecretStore.cs`): `Names` exposes key names only,
`TryGet` is the sole read path, call sites read through lazy accessors rather than holding a key.
Provider error paths parse the structured `error.message` field rather than logging a raw
response body.

## House conventions

### Comments and doc comments are terse

State what the code does and any constraint a caller must respect. Nothing else.

- **No decision logs.** Do not record why an alternative was rejected, what the design used to be,
  or which comment this one replaces.
- **No historical commentary.** "This used to…", "no longer…", "the previous approach…" — cut it.
  The code describes the present.
- **Minimal doc comments.** One line on a public member where the name is not enough. Skip
  `<param>`/`<returns>` that only restate the signature.
- **Issue numbers only where they carry live information** — a gate, a workaround still in force.
  Not as provenance for every line.

The tree was converted to this style in one pass: doc comments reduced to a single statement,
rationale paragraphs and historical commentary removed, `.csproj` decision logs deleted. Some
rationale was lost with them. If a constraint is subtle enough that it needs explaining, prefer a
test that fails when it is broken over a paragraph saying not to.

### Tests are named as behavioural sentences

Not `MethodName_Condition_Result`. From the suite: `ACancelledTurnIsNotAFailureTests`,
`AStaleBuildSaysSoTests`, `TwoUnpromptedVoicesKeepTheirDistanceTests`, `SayItAndTheShipDoesItTests`.

### Locate the repository root with `d47.slnx`

19 test files do. Two walk up for `CLAUDE.md`
(`tests/D47.Core.Tests/Knowledge/FindMaterialAsksTheOneTableTests.cs:111`,
`TheWordIsCraftExceptWhereItIsNotTests.cs:87`) and one for a docs page. Use `d47.slnx` — a build
file that must exist for the test to have compiled. Keying off documentation makes the test fail
when that file moves, which is what happened in this checkout.

### Generated data is generated

`tools/gen-*.py` produce `src/D47.Core/Knowledge/*.tsv` and
`src/D47.Core/Journal/MaterialGrades.g.cs`. Not part of the build, never run by CI; re-run by
hand when Frontier changes game data. Edit the generator, not the table.

### Documentation structure is load-bearing

`D47.Core.csproj` embeds `docs/capabilities/*.md` and `docs/*.md` as resources;
`Core.Help.HelpLibrary` parses them into the in-app help panel, taking the first `d47-eli5` band.
`tests/D47.Core.Tests/DocumentationGateTests.cs` fails the build when a registered capability has
no page, when a page quotes no real code block, or when a page does not quote the capability's
current tool schema.

### Vendored code is not edited

`src/D47.Vr/vendor/openvr_api.cs` is vendored and pinned by a version-constant test.

## Release and deployment

- **The asset contract.** `src/D47.App/Updates/UpdateChecker.cs` hardcodes `d47.zip` and
  `d47.zip.sha256`. Every install in the field polls for those exact names; renaming either
  breaks updates silently, with no error anywhere.
- `worker/` is the Cloudflare Worker backing donations — `wrangler deploy` from that directory,
  and its `node --test` suite is deliberately not part of `dotnet test`. Nothing in the .NET
  build references it.

## Repo layout

| Path | What |
| --- | --- |
| `src/` | The 8 projects. `D47.Core` is the hub; `D47.App` the composition root and UI. |
| `tests/` | 7 xunit suites plus `tests/fixtures/` journal fixtures. |
| `docs/` | Published Jekyll site. Also embedded into `D47.Core`. |
| `tools/` | The Python table generators and the release commands. |
| `worker/` | The Cloudflare Worker backing donations. |
| `installer/` | `d47.iss`, the Inno Setup script. |
| `assets/` | Cues, alert beds, ship art, icons. |
