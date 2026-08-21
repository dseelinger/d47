# d47

Elite Dangerous voice companion for Windows 11. .NET 10, C#. One widget tree renders to
both a desktop window and a SteamVR overlay.

The specs call the product **"TheApp"** — a placeholder. The repo and the working name are
**d47** (*Directive 47: Optimize Inferior Systems*).

## Read the spec, don't guess

Three documents carry the design. They are not summarized here — go read the relevant one.

| Question | File |
|---|---|
| What should it do? Is this in scope? | [list.md](list.md) — 160 items in 22 phases, each line carrying its own acceptance criteria |
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
- **"One widget tree renders to both surfaces" is shorthand.** It means one view definition
  instantiated twice against one view model — a `Visual` belongs to exactly one visual tree,
  so the framework will not do the literal thing. The VR path hosts its copy in a `Window`
  that is constructed and never shown — a real `UserControl` is templated, and detached from a
  logical tree it rasterises as an empty quad with no error. Minimise-safety does not rest on
  there being no window; it rests on the VR path never depending on the state of the window the
  Commander can see. See architecture.md D1, amended in Phase 9.
- **Feature parity between the two surfaces is a nice-to-have, not a constraint.** *Amended
  2026-08-19.* The invariant above is about the **mechanism** — one view definition, no second UI
  codebase, no screenshot of the desktop window — and that is untouched and still binding
  (architecture.md §1). What is **not** a rule is that both surfaces must show the same things.
  Settings has been desktop-only since Phase 12; Checklist and Loadout were withdrawn from the big
  VR panel on the Commander's instruction, undoing on purpose what Phases 25 and 26 built there.
  So a tab may live on one surface and not the other, and **VR reaching parity with the window is
  a someday-maybe** rather than something a design has to be bent around. Take what works quickly.
  The mechanism is already the default rather than something to build: a tab appears only where a
  host calls `Furnish`, and `PanelView.Tab` declines to select one nobody furnished. So a tab is
  withdrawn from a surface by *not* making that one call, and neither the drawn route nor the
  spoken one needs teaching a special case.
- **All audio goes through the one arbiter**, which exposes the render reference tap from day one.
- **No telemetry.** Permissive licenses only, no copyleft — verify the transitive graph, not
  just direct references. **This is a rule about code**, and it was applied to game data once
  by mistake, which cost accuracy in both directions: a source was rejected on licence grounds
  when its real problem was elsewhere, and two were accepted without anybody noticing whose
  data was inside them.
- **Game data is Frontier's, and d47 uses it under their published rules.** Ship figures,
  blueprints, material names and engineer locations are facts about Elite Dangerous, not about
  the community repository they were read from — coriolis-data says so outright, declaring its
  JSON Frontier's property with its MIT grant covering only code. So a source's own licence is
  checked and never mistaken for permission over the data underneath it. Three things follow.
  Tables are **derived by a generator with its provenance recorded**, never copied wholesale or
  hand-written. Frontier is **attributed in their own words** — see `NOTICE`, which is where the
  wording lives so it is written once. And the use stays **non-commercial**, which is the
  condition their rules attach; the code is MIT and the data is not d47's to relicense.

## Conventions

- **Structure only where it buys quality.** Projects exist to enforce a dependency boundary
  or to be independently testable — not to express a taxonomy. Add the seam when the phase
  that needs it arrives.
- **Surgical changes: every changed line traces to the task.** Match the surrounding style even
  where you would write it differently, and remove only the imports and helpers *your* change
  orphaned. Unrelated dead code gets mentioned, not deleted.
- Build and release stay frictionless: one command to build, one to test, one to publish.
  If a workflow needs a checklist to run, fix the workflow.
- Every registered capability needs a documentation page; CI enforces this.
- **Phase numbers are references, and phases 1-21 and 23-38 are frozen.** Several hundred code comments
  cite `list.md Phase N` to say why a thing exists — Phase 4 alone 55 times — so renumbering a built
  phase silently repoints them at the wrong item. Each phase joins the frozen set the day it ships —
  Phase 15 did so at 22 citations across 18 files, Phase 21 on 2026-08-16, Phase 23 on 2026-08-17,
  Phases 24 and 25 together on 2026-08-17, Phases 26, 27, 28 and 29 on 2026-08-18, Phases 30, 31, 32, 33 and 34 the same day, Phases 35 and 36 on 2026-08-19, and Phases 37 and 38 on 2026-08-20 —
  and the set only ever grows. **22 is a retired number, not a hole.** Phase 22 was cut on 2026-08-18 with nothing built,
  and it is **not reused**: a later phase renumbered into 22 would silently repoint every citation
  that ever said "Phase 22" at a subject it was never about, which is the failure the rule above
  exists to prevent arriving by a different road. It was also the only phase whose number was still
  free to move, so nothing is now movable at all.
  The unfinished tail was renumbered **twice on 2026-08-15** — once into build order, and once again
  when three items were pulled into a new Phase 16 so the running order is the numbers themselves
  rather than a paragraph explaining them. Both passes are recorded with their mapping in
  [docs/plans/build-order.md](docs/plans/build-order.md). They were cheap only because those phases
  were unbuilt. **From here new phases are appended**, and a phase that has shipped anything is not
  renumbered again. Moving items *between* unbuilt phases stays free and encouraged when a phase
  stops being one subject — a phase is a minor release, so one that cannot be finished holds its
  ready items hostage.
- **Renumbering: remap the numbers mechanically, then check the moved items by hand.** Both passes
  moved every number correctly and both got the same thing wrong: a citation naming an *item* that
  changed phase, which a faithful remap carries to a number that still resolves — to the wrong place,
  reported by nothing. Map through placeholders exactly once per file so no replacement can re-consume
  its own output, cover the prose forms (`Phases 17, 19 and 20` matches no pattern looking for
  `Phase` + space + digit), **check bare numbers in table cells** — a `| 16 |` in a phase column has
  no `Phase` next to it and has now been missed twice — and then re-read every citation of an item
  that moved. That last step is
  not automatable; it is the only one that matters.
- **Order within a phase is subject grouping; execution order lives in the plan.** `list.md`
  reads top to bottom as a description of the product, not as a schedule — Phase 14 shipped its
  ninth item as step 12 of 13. When sequence matters, it belongs in `docs/plans/`, which is where
  dependencies can be stated and argued rather than implied by position.

## Build

```
dotnet build          # net10.0-windows, warnings are errors
dotnet test           # includes the Core dependency boundary and the docs gate
dotnet publish src/D47.App -c Release      # self-contained d47.exe + runtimes\ natives, no flags needed
```

Release is a tag: `git tag -s v0.1.0 -m "what changed"` then `git push origin v0.1.0`
publishes, checksums and creates the GitHub Release. Publish settings live in
`D47.App.csproj` so local and CI cannot diverge.

**`tools/release.ps1 <patch|minor>` is that whole process, once.** It commits what is in the
working tree, merges the branch to `main`, runs `dotnet test -c Release`, pushes, **waits for CI
to go green**, works the next version out from the newest tag, and only then signs and pushes the
tag. The three waits are not politeness — each is a rule below that has already cost a version
number that could not be reused. Run it from any branch; `-Yes` skips the confirmation for an
unattended run.

**A completed phase is always a minor release.** Finishing a phase in `list.md` means the
next tag is `0.<minor+1>.0`, not another patch — the version is how a Commander tells "some
fixes landed" from "there is a whole capability here now". Fixes between phases are patches.

**A published tag never moves.** Tags are signed and annotated, and once one is pushed and a
Release is built from it, that tag is a receipt for one exact `d47.exe` and the checksum
beside it — and the update checker compares a running build's version against it. Retagging
makes one version number mean two different binaries, which is the one thing a version number
exists not to do. Fixes ship as the next patch release; releasing is one command, so there is
never a reason to reuse a number. This also means `dotnet test -c Release` has to pass
*before* tagging: the release workflow runs it, and a failed run leaves a published tag with
no Release behind it, which costs a version number to correct.

Three gates run as tests rather than as CI steps, so they cannot drift from the code:
`CoreDependencyTests` asserts Core references no UI, hardware or provider assembly,
`DocumentationGateTests` asserts every registered capability has a page under
`docs/capabilities/` that quotes its current tool schema, and `PackageLicenceGateTests`
asserts every package in the **transitive** graph declares a permissive licence. Change a
tool's schema and the docs test tells you what to paste; add a package with a copyleft
dependency four levels down and the licence gate names it and the chain that pulled it in.

The licence gate reads `project.assets.json` and each package's `.nuspec`, so it walks what
restore actually resolved rather than what a csproj asks for, and it adds no package of its
own. **It covers packages and nothing else** — it never enumerates a file in this repository
and cannot be pointed at one. `src/D47.Core/Knowledge/*.tsv` is deliberately out of its
scope: that data is Frontier's and is governed by their rules rather than by any package
licence, which is the distinction the invariant above records as having been got wrong once.
And it reads what a package *claims*: a nuspec declaring Apache-2.0 while packing an
LGPL binary is invisible to it, so it raises the floor rather than replacing the reading.

Everything the app writes goes to `data/` beside the executable — never `%APPDATA%`.

`D47_COVERAGE=1` records which tools and settings rows have actually been driven in the
running app, and which have changed since they last were, to `data/coverage.md`. A workbench
aid for knowing what is left to try by hand — off, and entirely absent from the surface,
unless that variable is set.
