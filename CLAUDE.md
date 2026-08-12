# d47

Elite Dangerous voice companion for Windows 11. .NET 10, C#. One widget tree renders to
both a desktop window and a SteamVR overlay.

The specs call the product **"TheApp"** — a placeholder. The repo and the working name are
**d47** (*Directive 47: Optimize Inferior Systems*).

## Read the spec, don't guess

Three documents carry the design. They are not summarized here — go read the relevant one.

| Question | File |
|---|---|
| What should it do? Is this in scope? | [list.md](list.md) — ~130 items in 17 phases, each line carrying its own acceptance criteria |
| How is it built? Why not X? | [architecture.md](architecture.md) — stack, dependency direction, trust boundaries, packaging |
| What do the personas say? | [guardian-personas.md](guardian-personas.md) — 11 Guardian cores plus the shared preamble |

Before proposing a stack change, check `architecture.md` §10 (rejected alternatives) and §1
(constraints). Most of the obvious alternatives were already considered and rejected for a
stated reason.

## Invariants

Each of these is cheap to break by accident and expensive to fix later.

- **Core depends on nothing.** No UI, no hardware, no providers. This is what makes the
  replay harness possible. Enforced by project references.
- **No Core component owns a thread or reads the clock.** Time is injected. The journal
  reader exposes `Poll()`; the tick loop calls it in production, the harness calls it at 100x.
- **Capability descriptors are registered once and never mutated.** Tool schemas must
  serialize byte-identically across turns (stable key order) or prompt caching dies.
- **Prompt assembly is ordered by volatility:** tools → guardrails → persona → About Me →
  *cache breakpoint* → history → game state. Guardrails sit **above** the persona so
  switching personality off cannot strip them.
- **Journal, in-game comms, web search and INARA are untrusted input.** Anything the model
  can call, a hostile in-game message can attempt to invoke.
- **Safety-critical settings are unreachable from the tool surface.** Protected is a property
  of the caller, not the modality — panel, hotkey and keyword router reach them; the LLM does not.
- **Key injection:** `SendInput` scancodes only. Never a `WH_KEYBOARD_LL` hook.
  `release_all()` is unconditional. Verify Elite is foreground before injecting.
- **Binds are read-only.** Never write the Commander's bindings file.
- **All audio goes through the one arbiter**, which exposes the render reference tap from day one.
- **No telemetry.** Permissive licenses only, no copyleft — verify the transitive graph, not
  just direct references.

## Conventions

- **Structure only where it buys quality.** Projects exist to enforce a dependency boundary
  or to be independently testable — not to express a taxonomy. Add the seam when the phase
  that needs it arrives.
- Build and release stay frictionless: one command to build, one to test, one to publish.
  If a workflow needs a checklist to run, fix the workflow.
- Every registered capability needs a documentation page; CI enforces this.

## Build

```
dotnet build          # net10.0-windows, warnings are errors
dotnet test           # includes the Core dependency boundary and the docs gate
dotnet publish src/D47.App -c Release      # one ~50 MB self-contained d47.exe, no flags needed
```

Release is a tag: `git tag v0.1.0 && git push --tags` publishes, checksums and creates the
GitHub Release. Publish settings live in `D47.App.csproj` so local and CI cannot diverge.

Two gates run as tests rather than as CI steps, so they cannot drift from the code:
`CoreDependencyTests` asserts Core references no UI, hardware or provider assembly, and
`DocumentationGateTests` asserts every registered capability has a page under
`docs/capabilities/` that quotes its current tool schema. Change a tool's schema and the
docs test tells you what to paste.

Everything the app writes goes to `data/` beside the executable — never `%APPDATA%`.
