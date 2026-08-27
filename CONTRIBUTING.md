# Contributing

This is one person's project, built in the open. Contributions are welcome and the bar is the
same one the existing code is held to — which is written down rather than implied, so you can
read it before spending an evening.

**If you are here to report a bug or ask for a feature, you probably want
[the Discord](https://dseelinger.github.io/d47/community.html) instead.** It reaches a person,
it needs no GitHub account, and the pinned template in `#bug-reports` asks for exactly what
makes a bug fixable. This page is for people who want to change the code.

## Read the spec first

Three documents carry the design, and they are not summarised here:

| Question | File |
|---|---|
| What should it do? Is this in scope? | [list.md](list.md) |
| How is it built? Why not X? | [architecture.md](architecture.md) |
| What do the personas say? | [guardian-personas.md](guardian-personas.md) |

[CLAUDE.md](CLAUDE.md) is the working agreement — invariants, conventions, and the reasons
behind them. Most "why is it done this odd way" questions are answered there, and
`architecture.md` §10 lists alternatives that were already considered and rejected, with the
reason. Checking those two before proposing a stack change saves everybody a round trip.

## Before you write code

**Open an issue first for anything non-trivial.** Not bureaucracy — scope. `list.md` is the
product description and it is opinionated; a well-built feature that does not belong is worse
than no feature, because declining it wastes work already done. An issue costs you five
minutes and gets you an answer before you spend the evening.

Small and obvious fixes — a typo, a wrong link, a crash with a one-line cause — go straight to
a pull request. Nobody wants a ceremony for those.

## What the code has to satisfy

```
dotnet build          # warnings are errors
dotnet test           # includes three gates that run as tests
```

Both must pass. Three of those tests are gates rather than unit tests, and each one will tell
you exactly what to do if you trip it:

- **`CoreDependencyTests`** — `D47.Core` references no UI, hardware or provider assembly. This
  is what makes the replay harness possible; it is not negotiable.
- **`DocumentationGateTests`** — every registered capability has a page under
  `docs/capabilities/` quoting its current tool schema. Change a schema and the test prints
  what to paste.
- **`PackageLicenceGateTests`** — every package in the *transitive* graph declares a permissive
  licence. No copyleft, and it names the chain that pulled a bad one in.

## House style

**Surgical changes: every changed line traces to the task.** Match the surrounding style even
where you would write it differently. Remove only the imports and helpers *your* change
orphaned — unrelated dead code gets mentioned in the PR, not deleted in it.

**Comments say why, not what.** The code in this repository explains its own reasoning at
unusual length, and that is deliberate: several of the odder decisions were made after
measuring something, and the measurement is recorded beside the code so nobody undoes it by
being reasonable. If your change rests on a fact about Elite, say where the fact came from.

**Game data is derived, never hand-written.** Ship figures, blueprints, material names and
engineer locations are Frontier's facts about their game. They are generated with their
provenance recorded, never copied wholesale and never typed in from memory. See the invariants
in `CLAUDE.md` before touching anything under `src/D47.Core/Knowledge/`.

**No telemetry.** Ever, anywhere, for any reason.

## Pull requests

Say what changed and why. If it fixes an issue, `Fixes #N` in the body closes it on merge —
that is how issues get closed in this repository, rather than by editing a list.

If your change is a bug fix, the most persuasive thing you can include is a test that fails
without it. Reintroducing the fault and watching the new test fail is the only way to know the
test tests the thing; twice it has shown a diagnosis to be wrong.

## Licence

The code is MIT. The Elite Dangerous game data shipped alongside it is Frontier's and is used
under their published rules for non-commercial use — see [NOTICE](NOTICE). Contributions are
accepted on those same terms.
