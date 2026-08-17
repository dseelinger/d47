# The enquiry to Frontier — draft

**A draft for the maintainer to review and send. d47 does not send mail, and nothing here has been
sent.** Written 2026-08-16 as the action item from
[screen-reading-licence-and-rules.md](../spikes/screen-reading-licence-and-rules.md) §6.6 and
decision 2 of [phase-22-reading-the-screen.md](phase-22-reading-the-screen.md).

Two routes exist and they are not alternatives — the form registers the tool, the email asks the
question:

- **`community@frontier.co.uk`** — named in the Elite Dangerous Media Usage Rules for the case of
  *"a purpose that isn't listed above"* where a creator "would like clarification on our position".
  This is the one that carries the question.
- **The [3rd Party Devs submission form](https://forums.frontier.co.uk/threads/3rd-party-devs-submission-form.349330/)**
  — a locked Frontier sticky for tool developers to register. Worth filing regardless of Phase 22,
  since d47 is a third-party tool today.

## Why it is worth asking rather than assuming

Frontier has published nothing on point. The EULA's literal text catches every journal reader ever
written, including the ones Frontier built the journal *for* and hosts release threads for on its
own forum, so the literal reading cannot be the operative one. The operative word is
**"unauthorized"** — and that is exactly the word only Frontier can settle.

The precedent is theirs, too, and it is the most encouraging fact in the whole investigation: when
tools were reading the network log, an unpublished channel, Frontier's answer was to publish the
journal and document it. Asking is the version of that conversation which starts before rather than
after.

**Keep the ask narrow.** The question is about reading rendered output locally, on the same machine,
for the player's own session. It is not about the API, not about servers, and not about publishing
imagery — the Media Usage Rules already cover that last one and d47 already complies.

---

## Draft — email to `community@frontier.co.uk`

**Subject:** Third-party tool: is reading the game's own screen locally within your rules?

Hello,

I maintain **d47**, a free, non-commercial, open-source voice companion for Elite Dangerous
(https://github.com/dseelinger/d47). Like EDMC, EDDI and others it reads the Player Journal and
`Status.json` — the channel your Player Journal manual documents for exactly this purpose. It sends
no telemetry, and it uses game data non-commercially with Frontier attributed, per the Media Usage
Rules.

I would like clarification on one thing before I build it, rather than after.

**The question.** For a small number of things the journal does not report, I am considering having
the tool capture its own machine's Elite window and read the rendered image locally — nothing is
uploaded, nothing is stored beyond the moment, no image or derived text leaves the player's
computer, and no data is shared with any third party or aggregated anywhere. The tool does not
modify the game, does not read its memory, does not inject input into it for this purpose, and makes
no requests to your servers. It reads pixels the player is already looking at, on their own screen,
during their own session.

Two concrete cases, which I think differ in how they sit with your rules:

1. **System names on the Galaxy Map**, so the tool can answer questions about systems the player is
   looking at. These are public astrography and the journal already records them for every system
   visited.
2. **The contacts panel** — so the tool can warn a player about who is around them, which the
   journal has no event for. This is the more valuable case for players and, reading clause 3(d) of
   the EULA, the one most likely to concern you, since it involves other characters.

**Why I am unsure.** Clauses 3(c) to 3(e) and 4.5 of the EULA read, taken literally, as prohibiting
this — but taken literally they equally prohibit every journal-reading tool, including the ones the
Player Journal was created to serve. So I read the operative word as *"unauthorized"*, and I would
rather ask what is authorized than decide it for myself.

I would be grateful for any of: yes, no, or yes-with-conditions — for instance if reading public
information such as system names is acceptable while anything concerning other players is not. A
short answer is plenty, and I will build to whatever it says. If there is a better route for this
question than this address, I would be glad to be pointed at it.

Thank you for the Player Journal, and for keeping the third-party tool community supplied for the
best part of a decade.

Best regards,

Doug Seelinger
d47 — https://github.com/dseelinger/d47

---

## If the answer is yes-with-conditions

Write the conditions into `list.md` beside the item, not only into this file. A condition recorded
only in a plan document is a condition that stops being enforced the day somebody builds from
`list.md` instead.

## If the answer never comes

That is a plausible outcome and it is not the same as a no. Record the date sent and the silence,
and the Galaxy Map case proceeds on its own reasoning — it never needed this answer. The contacts
panel stays unbuilt, which is where it is today, so an unanswered email costs the phase nothing it
had.
