# Design source for d47

## Where the design lives

The source of truth for d47's look is the **Directive 47** design system artifact:
https://claude.ai/code/artifact/60936446-dcb6-48f4-81b0-b33dea16afa0

The direction is design → code. The design system sets the look, the controls and the layout
patterns; the Avalonia app is built to match them through issues. Where the design system and the
code disagree on look, the design system is right. Behaviour, strings and data come from the code;
the text on the design system's cards and screens is placeholder.

The design system is canonical the way a written specification is. When something in it does not
work in practice during implementation, make the change in the code and then work it back into the
design system; do not stop to amend the design system first.

The Claude Design project **D47 Design System** is older than the artifact:
https://claude.ai/design/p/33498e27-9fc4-425e-b580-0425d82e4597

The original D47 project (`29844a40-ce9a-463a-8ee1-3cc3d4fd5b52`) is archived. Do not read from
it, and do not run `/design-sync` against either project: nothing is generated from the code and
pushed to Claude Design.

## The snapshot in the repository

`design/system/` is a read-only copy of the Claude Design project, pulled on 2026-09-26, and older
than the artifact. It is for reading and for rendering locally; it is not edited here. To refresh it, pull the project again and replace
the folder whole.

It holds the project's files as they are, with two differences:

- The font files in `design/system/fonts/` are copied from `assets/fonts/`. The project's copies
  are the same families and weights, and their licence files match the repository's byte for byte,
  but files over 256 KiB cannot be read out of the project in full.
- The four pasted screenshots under the project's `uploads/` are not included, for the same reason.

`design/ds-update/` holds the reasoning the design system was built from (`DS-UPDATE.md`,
`D47-VS-ELITE.md`); the project's README points there. `design/handoff/` holds settled handoffs for
individual screens.

`evidence/` in this folder holds the bloom reference frames from #377.
