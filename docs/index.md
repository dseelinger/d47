---
# The site is called Directive 47, so the home page cannot also be: the header already
# carries the name, and repeating it puts "Directive 47 | Directive 47" in the browser tab
# and a nav entry beside a logo that already links here.
title: Overview
group: General help
nav_order: 0
---

<!--
  The ELI5 band: big pictures, few words, with the reference material underneath it.

  No heading of its own — minima renders page.title as the h1 already, which is why no page
  here carries a hand-written one either.

  Two rules for editing this block, both about kramdown rather than about taste. No blank
  lines inside it, and never indent a line by four spaces or more: either one can end the raw
  HTML span early and leave half a diagram rendered as text. The site cannot be built without
  Ruby, so a mistake here shows up published rather than locally.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Directive 47 is a voice you can talk to while you fly.</p>
<section>
<h2><span class="num">1</span> The game keeps a journal. D47 reads it.</h2>
<svg viewBox="0 0 880 250" role="img" aria-label="Elite Dangerous writes a journal file, which Directive 47 reads">
 <rect x="20" y="45" width="220" height="140" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <path d="M130 64 L156 116 L130 104 L104 116 Z" fill="var(--accent)"/>
 <text x="130" y="146" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">ELITE DANGEROUS</text>
 <text x="130" y="168" text-anchor="middle" font-size="14" fill="var(--text-muted)">you, flying</text>
 <text x="290" y="98" text-anchor="middle" font-size="14" fill="var(--text-muted)">writes</text>
 <line x1="252" y1="118" x2="316" y2="118" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="330,118 314,110 314,126" fill="var(--accent-muted)"/>
 <rect x="340" y="45" width="200" height="140" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <rect x="415" y="62" width="50" height="62" rx="5" fill="var(--surface-alt)" stroke="var(--accent)" stroke-width="2"/>
 <line x1="426" y1="79" x2="454" y2="79" stroke="var(--accent)" stroke-width="2.5" stroke-linecap="round"/>
 <line x1="426" y1="93" x2="454" y2="93" stroke="var(--accent)" stroke-width="2.5" stroke-linecap="round"/>
 <line x1="426" y1="107" x2="444" y2="107" stroke="var(--accent)" stroke-width="2.5" stroke-linecap="round"/>
 <text x="440" y="146" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">THE JOURNAL</text>
 <text x="440" y="168" text-anchor="middle" font-size="14" fill="var(--text-muted)">a file, already on your PC</text>
 <text x="585" y="98" text-anchor="middle" font-size="14" fill="var(--text-muted)">reads</text>
 <line x1="552" y1="118" x2="606" y2="118" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="620,118 604,110 604,126" fill="var(--accent-muted)"/>
 <rect x="630" y="45" width="220" height="140" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <circle cx="740" cy="92" r="27" fill="none" stroke="var(--accent)" stroke-width="2.5"/>
 <circle cx="740" cy="92" r="16" fill="none" stroke="var(--accent-muted)" stroke-width="2.5"/>
 <circle cx="740" cy="92" r="6" fill="var(--accent)"/>
 <text x="740" y="146" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">DIRECTIVE 47</text>
 <text x="740" y="168" text-anchor="middle" font-size="14" fill="var(--text-muted)">knows where you are</text>
 <text x="440" y="222" text-anchor="middle" font-size="14" fill="var(--text-muted)">Nothing is added to the game. The journal is something Elite already writes.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> You talk. It talks back.</h2>
<svg viewBox="0 0 880 250" role="img" aria-label="You ask Directive 47 a question and it answers out loud">
 <text x="440" y="34" text-anchor="middle" font-size="14" fill="var(--text-muted)">hold a key, or just say its name</text>
 <path d="M250 96 Q440 44 630 96" fill="none" stroke="var(--accent)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="640,101 620,88 622,104" fill="var(--accent)"/>
 <rect x="40" y="86" width="210" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <circle cx="145" cy="121" r="13" fill="var(--text-muted)"/>
 <path d="M122 160 Q145 136 168 160 Z" fill="var(--text-muted)"/>
 <text x="145" y="182" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">YOU</text>
 <rect x="630" y="86" width="210" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <circle cx="735" cy="127" r="24" fill="none" stroke="var(--accent)" stroke-width="2.5"/>
 <circle cx="735" cy="127" r="6" fill="var(--accent)"/>
 <text x="735" y="182" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">D47</text>
 <path d="M630 186 Q440 238 250 186" fill="none" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="240,181 260,194 258,178" fill="var(--accent-muted)"/>
 <text x="440" y="246" text-anchor="middle" font-size="14" fill="var(--text-muted)">it answers, out loud</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> One panel. Two places to see it.</h2>
<svg viewBox="0 0 880 270" role="img" aria-label="One panel is drawn on the monitor and in the headset">
 <rect x="330" y="16" width="220" height="80" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="440" y="52" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">ONE PANEL</text>
 <text x="440" y="74" text-anchor="middle" font-size="14" fill="var(--text-muted)">built once</text>
 <path d="M380 100 L250 148" fill="none" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="238,153 256,142 258,157" fill="var(--accent-muted)"/>
 <path d="M500 100 L630 148" fill="none" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="642,153 624,142 622,157" fill="var(--accent-muted)"/>
 <rect x="40" y="152" width="200" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <rect x="103" y="170" width="74" height="46" rx="4" fill="var(--surface-alt)" stroke="var(--accent)" stroke-width="2"/>
 <line x1="128" y1="222" x2="152" y2="222" stroke="var(--accent)" stroke-width="3" stroke-linecap="round"/>
 <text x="140" y="243" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">YOUR MONITOR</text>
 <rect x="640" y="152" width="200" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <rect x="698" y="174" width="84" height="42" rx="14" fill="var(--surface-alt)" stroke="var(--accent)" stroke-width="2"/>
 <circle cx="719" cy="195" r="7" fill="var(--accent)"/>
 <circle cx="761" cy="195" r="7" fill="var(--accent)"/>
 <text x="740" y="243" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text)">YOUR HEADSET</text>
 <text x="440" y="266" text-anchor="middle" font-size="14" fill="var(--text-muted)">Not a copy of the window — the same panel, drawn twice.</text>
</svg>
</section>
</div></div>

## The details

A voice companion for Elite Dangerous on Windows 11. It reads the journal Elite already
writes, answers out loud, and renders the same panel to a desktop window and a SteamVR
overlay.

This site is the long form. In-app help is the short form: every settings row links to the
page that explains it.

### A way back

Every setting has a default, and you can always get back to it.

**A row you have changed grows a small ↺ beside its label.** Press it and that row goes back to
the way it shipped. Rows you have not touched do not have one, so the glyph doubles as a quiet
marker of what you have actually changed.

**A card you have changed grows a Reset** beside its heading. That puts back everything on that
card in one go, which is the useful gesture when something has gone wrong and you do not know
which of twenty-two rows did it.

Two things reset never touches:

- **Your API keys.** Forgetting a key means going and finding it again, so it is a separate act
  and never something a card reset sweeps up. Asking for a working Speech tab is not asking to be
  logged out of ElevenLabs.
- **Anyone else's settings.** On a row that is yours rather than the installation's — About Me,
  your character sheet, the core paired to your ship — reset means *stop having my own answer*, so
  the installation's value shows through again. Clearing the box by hand still means deliberately
  blank, which is a different thing and stays different.

**Directive 47 cannot reset anything by itself**, and that is deliberate rather than an
oversight. Reset writes safety-critical settings, and a single call that reached all of them at
once is exactly what the rule about protected settings exists to prevent. The panel can, your
keyboard can, and the model cannot.

### Where everything is

**The nav on the left is the whole site**, grouped the way the settings panel is grouped —
Foundation, Ship, Knowledge, Conversation, Voice, Interface, Acting on the game.

Two pages are worth reading before any capability page:

- [Installing](install.md) — getting a build, verifying it, and where it keeps its files
- [Talking to Directive 47](conversation.md) — the two answer paths, running with no model, and
  what each turn reports

### Capabilities

Every capability D47 registers has a page under **Capabilities** in the nav. That is enforced by
tests rather than by habit — CI fails if a registered capability has no page, if its page does
not quote the capability's real tool schema, if the page is filed under a different group from
the capability, or if any published page is unreachable from the nav.

There used to be a table here listing them. It listed sixteen of the thirty-three, which is what
a hand-maintained copy of a generated list does given a few months — so the nav, which is built
from each page's own front matter and checked against the registry, is the list now.

### Attribution

{{ site.frontier_attribution }}

Directive 47's own code is MIT-licensed. The Elite Dangerous game data it ships — ship and module
figures, blueprints, material names, engineer locations — is Frontier's, used under their
[media usage rules](https://forums.frontier.co.uk/threads/elite-dangerous-media-usage-rules.510879/),
and is not covered by that licence.

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="install.html"><span class="ct">Installing →</span><span class="cd">Getting a build, checking it is really ours, and where it keeps its files.</span></a>
<a class="card" href="conversation.html"><span class="ct">Talking to Directive 47 →</span><span class="cd">What happens between your question and its answer.</span></a>
<a class="card" href="transcript.html"><span class="ct">The Transcript page →</span><span class="cd">The page you land on: every control around the conversation, and what decides the answers.</span></a>
<a class="card" href="capabilities/help.html"><span class="ct">Everything it can do →</span><span class="cd">One page per capability. The nav on the left is the whole list.</span></a>
</div>
</div>
</div></div>
