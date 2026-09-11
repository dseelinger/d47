---
name: new-issue
description: Turn a dictated request into an issue an issue worker can take without kicking it back — read the code first, verify every claim against the tree and the real journals, decide what is obvious and put only the real ambiguities to the maintainer as concrete choices, size it, then show him the text and file it once he acknowledges. Files issues; changes no code. Use when the user invokes /new-issue, or says "new issue", "file an issue about this", "make an issue for", "write this up as an issue".
---

# New issue

You turn a request into an issue that an issue worker can take and finish without coming back.

`/new-issue the Route page should show checkboxes` has named the work. Start on it in that same
turn. Only a bare `/new-issue` waits: acknowledge in one line, stop, and start when the maintainer
says what it is about.

## Turn the voice on first

Once the instruction lands, the first step of the working turn is `/neural-voice New issue <subject>`,
the subject in a word or two — `/neural-voice New issue engineers` for a request about the Engineers
tab. Several of these sessions run at once and are told apart by ear, and the subject is what tells
them apart. Where the request has no obvious subject yet, `/neural-voice New issue` on its own.

It is a default, not a fixture: `/neural-voice off` stops it and the work carries on unchanged.

## The failure this exists to prevent

A dictated request is a starting point, not a specification. Filed as dictated, it reaches an issue
worker who finds it means two different things, or rests on data that does not exist, or is three
jobs — and kicks it back. That round-trip costs a whole session.

So: **the first telling is never the issue.** Read, verify, decide, size, then file.

## Read the code before you ask anything

Find the surface, the type and the method the request is about, and read them. Most of what looks
like ambiguity is answered in the tree.

Ask the maintainer only what the code cannot answer: what they want, not what is there. Arriving
with "I read `EngineerRoutePage.Refresh`; it draws these four things and none of them is marked —
which did you mean?" is a different conversation from "what do you mean?"

## Verify claims against evidence, never memory

Where the request or your answer to it turns on what Elite writes, go and look. The Commander's own
journals are on this machine:

```
%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\Journal.*.log
```

Hundreds of them, and they settle what an event carries, what its fields are named, and whether it
is written at all. `grep` them. An issue asserting a field that does not exist is worse than no
issue — it gets built.

The same applies to the tables under `src/D47.Core/Knowledge/`. Read the row, do not recall it.

`HandledEvents.cs` is the fastest check of all: `ActedOn` is what d47 folds into state,
`NarratedOnly` is what it can say a sentence about and nothing more. A request that needs a
`NarratedOnly` event needs new state, and that changes the size of the job.

## What to hunt for

Go looking for these. They are where dictated requests come apart.

- **Which thing is meant.** A noun in the request — "prerequisites", "the list", "the status" —
  often maps to two or three different things in the code. Name them and make the maintainer pick.
- **A state the request assumes is binary and is not.** Checked or unchecked, done or not done,
  on or off. Look for the third case: the one d47 cannot decide. Asking the maintainer to choose
  between two states when the code has three is how a lie gets built into the panel.
- **A literal where the codebase works by role.** "Green", "bold", "at the top". `Palette` colours
  by role and never by name, and there is no success colour in it. Offer the role that exists
  against adding a new one, and say what adding one costs.
- **Scope across surfaces.** The same information is often drawn in more than one place — a
  directory and a detail page, the desktop window and the VR overlay. Ask whether the change is one
  surface or all of them. Two surfaces disagreeing about how a thing looks is a defect filed later.
- **Whether the data exists.** The one that produces "this is too big". Before writing "show X",
  establish that d47 can know X. If it cannot, that is the real finding, and it outranks the
  original request.

## Decide what is obvious; ask only what is not

**A question that changes nothing is not diligence, it is noise**, and a maintainer who is asked
two of them stops reading the third. The test is not whether you have a recommendation — it is
whether a different answer would produce a different issue. Where every answer leads to the same
body, you are asking to look thorough: drop it, or state your reading as an assumption he can
correct.

Ask when the answer changes what gets built and the code cannot settle it. A recommendation does
not excuse you from asking: widening the scope, splitting the job, or specifying something other
than what was dictated are the maintainer's to approve, and those are worth asking even when one
option is plainly the better one.

## Ask in one batch, with a worked example

Put the questions as choices, not as open prose, and send them together rather than one at a time.
Four is the limit; two real ones beat four with fillers.

Every option says what it would cost, not only what it is. "Simplest, and the cost is that the two
middle lines assert something d47 does not know" is a choice the maintainer can make. "Option B" is
not.

**An abstract question gets an abstract answer, or none.** Where the choice is about how something
reads or draws, build the example out of real data — a named engineer, the actual lines, what each
option would put on screen — and show it. A table of the real states, or a preview per option.

When one answer comes back as a question rather than a choice, that is the maintainer finding
something you missed. Go and check it before answering. Re-ask that one question only; do not put
the settled ones again.

## Size it before filing

Once the questions are answered, ask what the work actually spans. A change that stays inside one
page is one issue. A change that needs new journal state, a new column in a generated table, or a
new seam in Core is a different size of job, and stapling it to a drawing change is exactly what an
issue worker balks at.

Split only when the honest answer is that it is two jobs. When it is:

- Say which part is the **build** and which is the **design**. A question like "how do we express
  this in a generated table" is a decision, not a task: that one is `design`, for the Architect.
- Make the small one land first and stand alone. It must not wait on the large one, and it must
  need no rework when the large one lands. Say so in both bodies.
- Cross-reference them by number after filing, in the body of each.

Where it is plainly two jobs, split it, file both and say so in one bold line. Ask first only where
it is genuinely arguable. Do not decide it silently, and do not split a single job into two to look
thorough.

## Everything you file is already eligible

`gh issue create` authenticates as `dseelinger`. What you file cannot be told apart from what the
maintainer filed, and the eligibility test is "either `dseelinger` opened it, or it carries
`ready`". So an issue you file is eligible for autonomous work the moment it exists.

**Never ask the maintainer to add `ready` to an issue you filed on their instruction.** That label
is for an issue somebody else opened on the repository, which he has read and vetted. Asking him to
vet his own request back to him is noise, and it implies work from a stranger could be scheduled
without him — it cannot, and that is the point of the label.

The obligation this creates is on you: file nothing you would not be content to see an issue worker
start on unread.

## Nothing reaches the tracker until he has acknowledged it

Every write to a GitHub issue — creating one, editing a title or a body, commenting, adding a label
— is shown to the maintainer in full and waits for him to say go.

Show the text you intend to write, not a summary of it: the title as it will read, the body as it
will read, the label, and which issue it lands on. Then stop and wait. An instruction to write the
thing up is not an instruction to file it, and a turn that ends without an answer files nothing.

His answer is one of three: file it, file it with the changes he names, or do not file it. Only the
first two reach `gh`.

One acknowledgement covers one write. Where the work is plainly two issues, that is two texts shown,
unless he acknowledges both in one reply. It does not carry forward to a later issue in the same
session, or to a correction to something already filed.

This rule outranks the rest of this skill. Where anything below says to file, to comment or to
label, it means: show it, wait, then do it. The bold line naming what you chose rather than asked is
shown with the text it applies to, not after the issue exists.

## The body

The repository's form, in its writing style — literal, terse, no metaphor standing in for a
statement. Read the CLAUDE.md writing rule before you write prose, and the `/prose` skill is there
if you want it checked.

- **Title**: the claim, and the mechanism or cause after a colon where one is needed.
- **A grounding paragraph** naming the code this rests on, with paths and symbols you have read.
  This is the part that stops it coming back.
- **`### What is wanted`** — the decision, in the maintainer's terms.
- **`### Accepted when`** — statements that can be checked, not intentions.
- **`### Notes for the build`** — the two or three things you found that would otherwise stall a
  worker: a property that does not exist yet, a constant that has to be added, a place the page
  will get long. Only what you verified.
- **`### Not this issue`** — the neighbouring work it will be confused with.
- **One label that already exists**: `bug`, `change-request`, `enhancement`, `documentation`,
  `accessibility`, `data-accuracy`, `design`, `vr`. Do not invent one.

Write the body to a file and pass `--body-file`. A heredoc mangles backslashes and long bodies.

## Say what you chose, in bold

Every call you made rather than asked — a widened scope, a third state where two were requested, a
role colour where green was, two issues where one was dictated — goes in your answer as **one bold
line**, with the reason in that line or the next, and the offer to revert. Where the issue itself
departs from the request, say it in the body too, in a paragraph of its own.

The maintainer objects by reading one line, or reads nothing and it stands. So state the choice,
give the reason once, and stop. Do not bury it in a paragraph of reasoning and do not re-argue it.

An issue that quietly improves on the request is an issue the maintainer did not approve.

## What you never do

You never change the working tree, never close or relabel an issue you did not file, and never
start the work. You never write to the tracker without the acknowledgement above. You read, you
verify, you ask, you show, you file.

## Output

1. **Each issue filed** — number, title, label, and a line on what it covers. Link them.
2. **Every choice you made rather than asked**, one bold line each, with the reason and the offer
   to revert.
3. **What you found that the request did not know** — the thing that changes what gets built, in
   one or two sentences. This is often the most valuable part of the session.

Short. The issue carries the detail; this is the part the maintainer reads standing up. No closing
summary of the process, and no restating the grounding paragraph he can read in the body.
