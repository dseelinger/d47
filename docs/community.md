---
title: Community
group: General help
nav_order: 4
---

# Community

Directive 47 is one person's project with a handful of users. The Discord is where those
people are — it is the fastest way to get an answer, and the only place where "it does this
odd thing" turns into a fix without you having to write a formal report first.

<!--
  THE INVITE LIVES IN _config.yml AS discord_invite.

  This page and the site footer both read it, so it is the single line to edit if the invite
  ever has to be revoked and reissued. The README and the Community row in the app's About
  area link to this page, not to the invite: putting the invite in the app would bake it into
  every build ever installed, and a revoked invite would then be a dead button that only a
  new release could fix.
-->

**[Join the Discord]({{ site.discord_invite }})**

## What to use it for

**Questions.** `#questions` — how to make it hear you, what a setting does, why it said that.
No question about Directive 47 is too small for that channel.

**Bugs.** `#bug-reports`, following the pinned message. It asks for four things — your version,
what you did, the log, and the Windows event-log entry if it crashed. That last one is what
turns "it doesn't work" into a named line of code, and it names the line of code that failed.

**Screenshots.** `#screenshots`. Directive 47 is a thing you fly with, and the shots are the
best argument for it that exists.

**Ideas.** `#ideas` — what you want it to say, what you want it to notice, what it should do
that it doesn't.

## One thing to leave out

**Please don't paste a whole journal file.** Elite's journals carry your Commander name, your
account ID and everywhere you have been. The error line is enough, and the pinned template
says which lines to take.

## And one thing that does the scrubbing for you

If a report needs more than an error line, **don't paste it anywhere — press "Help improve D47"**,
which sits on the Log File, Journal and Raw journal readings and opens the same page from each. It
replaces your Commander name and Frontier IDs with stand-ins, drops other players' messages, and
then shows you the whole thing before anything leaves. One window, two shapes: a scrubbed excerpt
of the minutes around the moment something went wrong (ten by default, with a span chooser for longer), or — with *Include journal history*
on — your journal history at the scale you choose. [Donate your journals](donate-journals.html)
walks through it step by step.

**Where it goes is not a public issue.** A comment on a public repository is
mirrored by third-party archives and mailed whole to everyone watching, so
"ask and it will be deleted" is a promise no public transport can keep — by anybody, not just by
this project. Directive 47 has its own store, and a delete there is a delete:
**Settings › Privacy and this install › Privacy and egress**, group What D47 keeps, row Your donation identifier, button **Forget it, and delete what was sent** (an advanced row, shown with **Show every setting**) asks it to delete everything you
have ever sent, without you having to post anywhere or ask anyone. See
[Privacy](capabilities/privacy.html) for what is scrubbed and what is kept.

## Where the work is tracked

Fixes and features live in [GitHub Issues](https://github.com/dseelinger/d47/issues), which is
public and readable by anyone. You are welcome to read it, and you never have to use it — a
report in Discord reaches the same place, and reaches a person on the way.

If you want to **contribute code**, [Issues](https://github.com/dseelinger/d47/issues)
is the right front door.

## In the app

D47 mentions the Discord about once every 14 days. The **Mention the Discord every two weeks**
switch in Callouts turns that off. The setup guide's last step has a **THE D47 DISCORD** link, and
the About page has a Community row that opens this page.
