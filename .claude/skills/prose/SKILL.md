---
name: prose
description: Review the prose in a change against the CLAUDE.md writing style rule — comments, doc comments, the CHANGELOG entry, the commit message and any touched docs page. Flags metaphor and flourish standing in for direct statement, and offers a literal rewrite for each. Use when the user invokes /prose, or says "check the prose", "is this mannered", "review my comments for style", "did I write anything mannered".
---

# Prose review

CLAUDE.md states the rule this skill enforces:

> Mannered prose substitutes metaphor and flourish for direct statement. [...] The phrases exist to
> display the writer, not to convey the idea, and readers can tell. [...] It is also imprecise.
> Metaphors drag in connotations the writer did not choose and cannot control. The fix is to say
> what you mean. When a literal phrase is available, use it.

The built-in `/code-review` does not check this — it reviews correctness, reuse, simplification and
efficiency. That is why mannered writing reaches main. This pass is the check.

## What to read

Default target is the working diff: `git diff HEAD`. With an argument, take that instead — a path, a
commit, or a range such as `HEAD~3..HEAD`.

Within the target, read only prose **added or changed by this diff**:

- `//` and `///` comments in `src/` and `tests/`
- the CHANGELOG.md entry
- the commit message, when reviewing commits rather than the working tree
- `docs/**/*.md`
- user-facing strings the app speaks or displays

Do not review lines the diff did not touch. Pre-existing mannered prose is not this change's problem.

## What the rule does not cover

CLAUDE.md exempts **code identifiers and established technical terms**. So:

- Type, method, field and variable names are out of scope, however figurative.
- Test names are identifiers. `TwoUnpromptedVoicesKeepTheirDistanceTests` is the house convention,
  not a finding.
- Domain vocabulary is literal here. This project has an Adventures feature, so "the story" names a
  real object. Ship, hull, beat, scene, callout, panel, overlay and the rest are the subject matter.
- Terms of art are terms of art: a UI class *paints*, a poller has a *cadence*, an event *fires*, a
  cache is *warm*, a lock is *held*. These have no shorter literal equivalent and carry no unwanted
  connotation.

## The test to apply

For each candidate phrase, in order:

1. **Is there a literal word for this?** If yes, the figurative phrase is mannered. "The lever on
   apparent text size" has one: width. "Knob" has one: setting, or parameter.
2. **Does the metaphor drag in connotations the author did not choose?** "Guardrail" implies a crash
   is expected. "Marries" implies symmetry between two things that may not be symmetric.
3. **Does the flourish carry information?** Cut it and read the sentence again. If nothing was lost,
   it was there to display the writer.
4. **Would a reader have to decode it?** Prose that makes the reader work so the writer can perform
   is the failure the rule names.

If a literal reading of the phrase is plausible in context, do not flag it. Prefer silence to noise:
a pass that flags twenty phrases teaches the reader to skip the output.

## Calibration, from this repository

Real findings, with the rewrite each should have had:

| Where | Written | Say instead |
| --- | --- | --- |
| `src/D47.App/Headset/VrPanelSurface.cs:21` | "512 wide rather than 640 because that is the lever on apparent text size" | "...because width sets apparent text size" |
| `src/D47.Knowledge/SpanshTradePlanService.cs:451` | "Every knob that reaches the request server-side" | "Every parameter that reaches the request server-side" |
| `src/D47.Core/Configuration/SettingsStore.cs:146` | "is now one knob for both" | "is now one setting for both" |
| `src/D47.Llm/OpenAi/ChatCompletionsLlmProvider.cs:427` | "would drop the effort router's lever" | "would drop the field the effort router sets" |

Near misses that are correct as written, and must not be flagged:

- `src/D47.Core/Adventures/*` — "the story", "the beat", "the scene". Domain objects.
- `src/D47.Core/Speech/LetterToSound.cs:174` — "sing", "long". Phonetic examples, quoted literally.
- `src/D47.App/Windowing/OverlayPanel.cs:132` — "the panel paints its own background". Rendering.
- `src/D47.App/Input/ScancodeInjector.cs:66` — "Elite does not rewrite Status.json on a heartbeat".
  Borderline. "On a fixed interval" is more precise, but heartbeat is standard for polling cadence.
  A judgement call, so say so rather than asserting it is wrong.

## While you are here

CLAUDE.md's "Comments and doc comments are terse" section names three more failures that reach the
same lines and slip through for the same reason. Report them **under a separate heading** so they
stay distinguishable from mannered prose:

- **Decision logs.** Why an alternative was rejected, what the design used to be.
- **Historical commentary.** "This used to...", "no longer...", "the previous approach...".
- **Doc comments that restate the signature.** `<param>`/`<returns>` adding nothing to the name.

`SettingsStore.cs:146` above is both at once — "was one of the six settings each surface kept a copy
of, and is now one knob for both" is a mannered phrase inside a historical note.

## Output

Group by file, most severe first. Per finding:

```
src/D47.Core/Foo.cs:88
  "the dial worth turning"
  Metaphor for a parameter. There is a literal word.
  → "the parameter worth varying"
```

Then one line saying how many lines of added prose were read. If nothing was found, say that plainly
and stop — do not pad the report with the near misses you considered.

Do not edit files unless the user asked for the fixes to be applied. When they do, apply the rewrites
you proposed and nothing else; a prose pass that also restructures comments is no longer reviewable.
