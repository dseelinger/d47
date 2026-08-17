# Phase 22 — Reading the screen

The plan of record for list.md Phase 22. Written 2026-08-16, after both halves of the phase's own
spike: [screen-reading-licence-and-rules.md](../spikes/screen-reading-licence-and-rules.md), which
was desk research, and [mirror-panel-locatability.md](../spikes/mirror-panel-locatability.md), which
is the instrument for the measurement that decides the phase and has not been taken yet.

`list.md` reads top to bottom as a description of the product. This is the order the work happens
in, and the reasoning the order cannot carry on its own.

---

## This plan's first job is to not build things

Every other phase plan in this directory sequences work. This one mostly declines to.

Phase 22's own first item says it is **"ordered first and deliberately dumb, because everything
below is moot if this fails"**, and the build order document puts the whole phase last for the same
reason: it is *"the only phase whose spike might close it outright"*. Those sentences are load
bearing, and the way to honour them is to build the instrument, take the measurement, and only then
write the next plan — not to build a Galaxy Map reader against pixels nobody has looked at and find
out afterwards.

So the deliverable of this pass is `spike/MirrorProbe` and the two documents it feeds, and the
deliverable of the *next* pass depends on a number that does not exist yet.

**Decided 2026-08-16 by the maintainer**, against the alternative of building the product plumbing
in the same pass. The plumbing is small, real and measurement-independent — `GuiFocus` is not parsed
today, and the `Poll()`-shaped seam has an obvious shape — but building it now would mean the phase
had shipped code before its gating question was asked, which is the specific habit this phase's
ordering exists to prevent.

## What the desk spike had already decided

Three things arrived settled, and each removes a choice rather than adding one.

1. **The computer-vision path is licence-clean and it is one package pair, not a family.** Emgu.CV
   is GPL-3.0 and out permanently, its paid escape hatch closed by maintainer decision the same day.
   `OpenCvSharp4.runtime.win` declares Apache-2.0 and packs an LGPL-2.1 FFmpeg binary twelve lines
   below the declaration, with `OPENCV_ENABLE_NONFREE ON` compiling in SURF. The slim runtime drops
   both problems because both are consequences of `videoio` and `contrib`, and it keeps every module
   the technique needs.
2. **No published Frontier rule addresses reading the screen either way**, and no further reading
   will change that. The EULA's literal text prohibits every journal reader Frontier built the
   journal for, so the literal reading cannot be the operative one; the operative word is
   *unauthorized*, and the journal is authorized in Frontier's own manual while the screen is not.
3. **The risk is not uniform across the phase.** EULA 3(d) names *"information about others …
   including about a character or the game environment"*, which is the contacts panel almost word
   for word and the Galaxy Map barely at all. That reverses `list.md`'s ordering.

## The three calls this plan makes

### 1. The measurement gates the phase, and nothing ships before it

Above. The instrument exists; the frames do not.

### 2. Frontier gets asked, and the contacts panel waits for the answer

The desk spike found a named route — the 3rd Party Devs submission form and
`community@frontier.co.uk`, which the Media Usage Rules point at for exactly the case of a purpose
that is not listed — and observed that **nobody has used it for this question**. It costs an email
and settles a question that no amount of reading can.

**Decided: ask.** The enquiry is drafted at
[phase-22-frontier-enquiry.md](phase-22-frontier-enquiry.md) for the maintainer to send; d47 does
not send mail on anybody's behalf. Until there is an answer, **nothing in this phase reads other
characters** — the contacts panel is not built, and Phase 15's rival-Power warning stays unreachable
exactly as Phase 15 wrote it.

This is not treating the EULA's text as decisive. It is noticing that the one case worth the most is
also the one the text names most directly, that the two facts were not previously known together,
and that there is a cheap way to stop guessing.

**The Galaxy Map case is not gated on the answer** and goes first if the measurement allows it: a
system name is public astrography that the journal already writes for every system visited, and that
every third-party galaxy index carries.

### 3. The OCR engine is `Windows.Media.Ocr`, and Phase 21 already paid for it

The desk spike recorded the OS engine as a lead rather than a recommendation, and gave one reason:

> d47's shared TFM is `net10.0-windows` (`Directory.Build.props:7`), and reaching WinRT projections
> needs a platform-versioned TFM such as `net10.0-windows10.0.19041.0` — a repo-wide change, not a
> package reference.

**That objection is gone, and it was already gone when the sentence was written.** Phase 21 moved
`D47.App` to `net10.0-windows10.0.26100.0` for `Windows.Gaming.Input`, on the same day, and paid the
6.4 MB for it. The change is not repo-wide and never needed to be: `Directory.Build.props` still
says `net10.0-windows` and every other project still uses it, because **App is the only project that
would host an OCR call anyway** — Core depends on nothing, and a screen reader is hardware.

So the OS engine costs a `using` directive rather than a framework migration, carries no third-party
licence, and leaves the ONNX Runtime / Eigen MPL-2.0 judgement undecided — which is where the desk
spike wanted it, since deciding it now would be deciding it without a use case.

**Unmeasured, and named as such:** nothing has established that `Windows.Media.Ocr` reads Elite's
font. That is a question for the frame the measurement produces, and it is cheap to answer once one
exists.

## What happens next, both ways

**If a panel is locatable in the mirror** — Galaxy Map first, in App behind an interface, with Core
seeing decoded results through a `Poll()` in the same shape as the journal reader and the HOTAS
reader, so the replay harness can drive a screen read with no game running. `GuiFocus` gates it:
nothing captures unless `Status.json` says a panel is open, so the subsystem is asleep except in the
seconds it matters.

**If it is not** — the same feature, desktop-only, and `list.md`'s own words for what that is worth:
*"a feature that works for half the Commanders who would want it"*. The half it does not work for
includes this repository's maintainer, which is a fact about how much of Phase 22 is then worth
building and is better faced with the number in hand.

Either way the phase stays last, and either way it ends up being the first input d47 reads that the
game did not deliberately write down.

## One piece of product work the measurement cannot change

**`GuiFocus` is not parsed today.** `GameStatus` in Core reads `Flags`, `Flags2`, fuel, cargo, heat,
body name, balance and position, and stops; the field that says which panel is open is in the file
and is read by nothing. It is the gate for every version of this phase — desktop or VR, Galaxy Map
or contacts panel — and it is a handful of lines against a channel Frontier publishes and documents.

It is **not** built in this pass, deliberately, under the first call above. `spike/MirrorProbe` reads
the field itself and writes it into every capture sidecar, which is enough to exercise the gate
while the phase is still deciding whether it has a gate to build.

## Where the spike changed the desk spike's answers

Neither is a contradiction and both are recorded in
[mirror-panel-locatability.md](../spikes/mirror-panel-locatability.md): the recommended package
*name* pulls in three packages nobody wants, and SIFT is not in the namespace its siblings are in.
The first changes what `MirrorProbe.csproj` references; the second would have cost five minutes to
anybody following the recommendation, once.

Both were found by restoring and compiling rather than by reading, which is the same shape as the
desk spike's own headline finding about a licence expression that describes only the package
author's code.
