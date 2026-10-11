---
title: Choosing providers
group: General help
nav_order: 3
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published rather than locally.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">On your computer, or on a service. That is the whole choice.</p>
<section>
<h2><span class="num">1</span> Where the work happens.</h2>
<svg viewBox="0 0 880 180" role="img" aria-label="Local providers run on this computer; hosted providers run on a service">
 <rect x="30" y="30" width="380" height="120" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="220" y="76" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">ON YOUR COMPUTER</text>
 <text x="220" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">free, no key</text>
 <text x="220" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">slower, plainer</text>
 <rect x="470" y="30" width="380" height="120" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="76" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">ON A SERVICE</text>
 <text x="660" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">usually a key and a bill</text>
 <text x="660" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">you pay the provider</text>
</svg>
</section>
</div></div>

Prices change, so none are quoted here; each table links the provider's own pricing page. *Free*
means D47 never bills you for it. *Pay per use* means the provider does. A provider that runs on
this computer keeps that work on this computer. Setting up for the first time is covered in [First run](first-run.html).

## Conversation {#conversation}

Which AI writes D47's replies.

| Provider | Cost | Key | Runs on this computer |
| --- | --- | --- | --- |
| None (local only) | Free | None | Yes |
| [Anthropic](https://www.anthropic.com/pricing) | Pay per use | Required | No |
| [OpenAI](https://openai.com/api/pricing/) | Pay per use | Required | No |
| OpenAI-compatible endpoint | Set by the server | Optional | If the server is on this computer |

- **None (local only)** suits a Commander who wants no model and no bill. D47 answers what it understands
  and says so when it cannot.
- **Anthropic** is the default and suits a Commander who wants Claude's answers.
- **OpenAI** suits a Commander who wants one key to cover conversation, voice and listening.
- **OpenAI-compatible endpoint** suits a Commander running Ollama or LM Studio, or a gateway they
  trust. Set its address in Settings.

## Voice {#voice}

How D47 sounds.

| Provider | Cost | Key | Runs on this computer |
| --- | --- | --- | --- |
| Edge Neural | Free | None | No |
| Kokoro | Free | None | Yes |
| Chatterbox | Free | None | Yes, on the processor |
| [ElevenLabs](https://elevenlabs.io/pricing/api) | Pay per use | Required | No |
| [OpenAI](https://openai.com/api/pricing/) | Pay per use | Required | No |
| [Cartesia](https://cartesia.ai/pricing) | Pay per use | Required | No |
| None | Free | None | Yes |

- **Edge Neural** is the default and suits a Commander who wants a voice with no account.
- **Kokoro** is free, with a small download.
- **Chatterbox** is free, with a larger download.
- **ElevenLabs** suits a Commander who wants the widest choice of voices.
- **OpenAI** and **Cartesia** suit a Commander who already holds that provider's key.
- **None** suits a Commander who reads replies.

## Listening {#listening}

How your speech becomes text.

| Provider | Cost | Key | Runs on this computer |
| --- | --- | --- | --- |
| This computer | Free | None | Yes |
| [Groq](https://groq.com/pricing) | Pay per use | Required | No |
| [OpenAI](https://openai.com/api/pricing/) | Pay per use | Required | No |
| [Deepgram](https://deepgram.com/pricing) | Pay per use | Required | No |
| [ElevenLabs](https://elevenlabs.io/pricing/api) | Pay per use | Required | No |

- **This computer** is the default, free and with no key.
- **Groq** suits a Commander whose computer is slow at Whisper.
- **OpenAI** and **ElevenLabs** suit a Commander who already holds that key for conversation or
  voice.
- **Deepgram** suits a Commander who wants a separate transcription service.
