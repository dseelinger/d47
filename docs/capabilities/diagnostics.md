---
title: Diagnostics
group: Foundation
nav_order: 101
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.

  This band is what the Technical and Log file readings of the Transcript page open, so it
  leads with those two rather than with the settings rows.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">The page that still answers when nothing else does — what happened, where it is written down, and how loud.</p>
<section>
<h2><span class="num">1</span> Two readings, and they are not the same thing.</h2>
<svg viewBox="0 0 880 250" role="img" aria-label="Technical shows the working behind one turn; Log file shows what the whole application wrote to disk">
 <rect x="20" y="30" width="410" height="150" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="225" y="70" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">TECHNICAL</text>
 <text x="225" y="104" text-anchor="middle" font-size="15" fill="var(--text-muted)">one turn, taken apart:</text>
 <text x="225" y="128" text-anchor="middle" font-size="15" fill="var(--text-muted)">what answered, what it looked up,</text>
 <text x="225" y="152" text-anchor="middle" font-size="15" fill="var(--text-muted)">and what it cost</text>
 <rect x="450" y="30" width="410" height="150" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="655" y="70" text-anchor="middle" font-size="18" font-weight="800" fill="var(--text)">LOG FILE</text>
 <text x="655" y="104" text-anchor="middle" font-size="15" fill="var(--text-muted)">the whole application, on disk:</text>
 <text x="655" y="128" text-anchor="middle" font-size="15" fill="var(--text-muted)">startup, the game, the headset,</text>
 <text x="655" y="152" text-anchor="middle" font-size="15" fill="var(--text-muted)">every part at once</text>
 <text x="440" y="216" text-anchor="middle" font-size="15" fill="var(--text-muted)">"Why did it say that" is the left one. "Why will it not start" is the right one.</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">The log is a file, so clearing is refused there — that control would be offering to delete it.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> It answers with nothing else working.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="Diagnostics needs no game, no model, no microphone and no headset to answer">
 <rect x="20" y="30" width="200" height="82" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="120" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text-muted)">NO GAME</text>
 <text x="120" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">running</text>
 <rect x="234" y="30" width="200" height="82" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="334" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text-muted)">NO MODEL</text>
 <text x="334" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">configured</text>
 <rect x="448" y="30" width="200" height="82" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="548" y="66" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text-muted)">NO MIC</text>
 <text x="548" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">and no headset</text>
 <rect x="662" y="30" width="198" height="82" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="761" y="66" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">STILL ANSWERS</text>
 <text x="761" y="92" text-anchor="middle" font-size="14" fill="var(--text-muted)">every time</text>
 <rect x="20" y="132" width="840" height="72" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="162" text-anchor="middle" font-size="16" fill="var(--text)">Ask "what's your status" and it names the version and every folder it writes to.</text>
 <text x="440" y="188" text-anchor="middle" font-size="15" fill="var(--text-muted)">Those paths are what a bug report needs, which is why they come back without being asked for separately.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Turn up one part, not the whole thing.</h2>
<svg viewBox="0 0 880 258" role="img" aria-label="Eight parts of the application each carry their own log level, changed by voice and taking effect on the next line">
 <rect x="20" y="30" width="200" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="120" y="62" text-anchor="middle" font-size="15" fill="var(--text-muted)">App</text>
 <rect x="234" y="30" width="200" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="334" y="62" text-anchor="middle" font-size="15" fill="var(--text-muted)">Capabilities</text>
 <rect x="448" y="30" width="200" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="548" y="62" text-anchor="middle" font-size="15" fill="var(--text-muted)">Settings</text>
 <rect x="662" y="30" width="198" height="52" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="761" y="62" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">Journal · Trace</text>
 <rect x="20" y="94" width="200" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="120" y="126" text-anchor="middle" font-size="15" fill="var(--text-muted)">Llm</text>
 <rect x="234" y="94" width="200" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="334" y="126" text-anchor="middle" font-size="15" fill="var(--text-muted)">Voice</text>
 <rect x="448" y="94" width="200" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="548" y="126" text-anchor="middle" font-size="15" fill="var(--text-muted)">Vr</text>
 <rect x="662" y="94" width="198" height="52" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="761" y="126" text-anchor="middle" font-size="15" fill="var(--text-muted)">Input</text>
 <rect x="20" y="166" width="840" height="72" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">"Turn journal logging up to debug" — in effect on the next line written, with no restart.</text>
 <text x="440" y="222" text-anchor="middle" font-size="15" fill="var(--text-muted)">Name a part that does not exist and it lists the ones that do, rather than doing nothing quietly.</text>
</svg>
<p class="body">Trace grows a log file fast. Turn the part back down once you have what you came for — the row and the spoken phrase are the same change, and both survive a restart.</p>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="../transcript.html"><span class="ct">The Transcript page →</span><span class="cd">The other reading, and every control around all three.</span></a>
<a class="card settings" href="conversation.html"><span class="ct">Language model →</span><span class="cd">Where the figures on the Technical reading come from, and what they cost.</span></a>
<a class="card" href="privacy.html"><span class="ct">Privacy →</span><span class="cd">What is in a log, and what never reaches one.</span></a>
</div>
</div>
</div></div>

## The details

Where Directive 47 keeps its files, and how much detail it writes down about what it is doing.

This is the one to reach for when something else is misbehaving. It needs nothing else to be
working — no game running, no model configured, no microphone, no headset — so it can still
answer when nothing else can.

### Ask for it

> "what's your status"
> "turn journal logging up to debug"
> "set voice logging back to information"

### What it tells you

```text
Directive 47 0.1.0
Installed at: C:\Tools\d47
Writable data: C:\Tools\d47\data
Logs: C:\Tools\d47\data\logs
Log levels:
  App: Information
  Capabilities: Information
  Settings: Information
  Journal: Information
  Llm: Information
  Voice: Information
  Vr: Information
  Input: Information
```

The version, where everything writable lives, and how loud each part of the app currently is.
The paths are the useful part when you are looking for a log to send with a bug report.

### Turning the detail up

Ask for more detail from one part of the app and it takes effect on the next line written —
no restart, and nothing to reload:

> "turn journal logging up to debug"

```text
Journal logging is now at Trace.
```

Ask for a part that does not exist and you are told what does, rather than nothing happening:

```text
'Telepathy' is not a valid subsystem. Expected one of: App, Capabilities, Settings, Journal, Llm, Voice, Vr, Input.
```

#### The parts you can turn up

| Part | Covers |
|---|---|
| `App` | Startup, composition, the window |
| `Capabilities` | The registry and tool dispatch |
| `Settings` | Settings and secret stores |
| `Journal` | Journal and `Status.json` reading |
| `Llm` | Model calls and token accounting |
| `Voice` | Capture, transcription, synthesis, the audio arbiter |
| `Vr` | OpenVR overlays and texture submission |
| `Input` | Key injection and binds parsing |

#### How loud

`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. `Trace` is everything;
`None` silences that part entirely.

Both log files get the same detail — `d47-<date>.log` to read and `d47-<date>.jsonl` to search
or hand to something that parses JSON. Turning a part up to `Trace` affects both, and `Trace`
grows a log file quickly, so it is worth turning back down once you have what you needed.

### Settings

| Row | What it does |
|---|---|
| Default log level | Applies to any part without its own level |
| *&lt;Part&gt;* log level | One row per part, offering the same levels |

Asking out loud and changing the row do the same thing, and both survive a restart. There is no
second, temporary kind of change to keep track of.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

The model reaches this capability through two tools. The schemas below are the current ones,
quoted from the registry — the documentation gate fails the build if they drift.

#### `get_app_status`

Reports the version, where writable files live, and the current level of every subsystem.
Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `set_log_verbosity`

Changes one subsystem's minimum level. Serilog reads the level switch on every event, so the
change applies to the next log line — there is no restart and no config reload.

```json
{"type":"object","properties":{"level":{"type":"string","description":"The new minimum level. Trace is the most detailed; None silences the subsystem.","enum":["Trace","Debug","Information","Warning","Error","Critical","None"]},"subsystem":{"type":"string","description":"Which subsystem to change the log level for.","enum":["App","Capabilities","Settings","Journal","Llm","Voice","Vr","Input"]}},"required":["level","subsystem"],"additionalProperties":false}
```

Both arguments are closed vocabularies, declared once and emitted into the schema as `enum`,
and checked before the handler runs — which is why an invented subsystem comes back with the
real list rather than being silently ignored.

</details>
