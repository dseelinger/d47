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
<svg viewBox="0 0 880 180" role="img" aria-label="Local providers keep your data here; hosted providers receive it">
 <rect x="30" y="30" width="380" height="120" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="220" y="76" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">ON YOUR COMPUTER</text>
 <text x="220" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">free, no key, nothing sent</text>
 <text x="220" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">slower, plainer</text>
 <rect x="470" y="30" width="380" height="120" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="76" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">ON A SERVICE</text>
 <text x="660" y="104" text-anchor="middle" font-size="14" fill="var(--text-muted)">usually a key and a bill</text>
 <text x="660" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">what you send goes to them</text>
</svg>
</section>
</div></div>

Prices change, so none are quoted here; each table links the provider's own pricing page. *Free*
means D47 never bills you for it. *Pay per use* means the provider does. For exactly what each
choice sends, open **Privacy and egress** in Settings: it is computed from your current settings.
Setting up for the first time is covered in [First run](first-run.html).

## Conversation {#conversation}

Which AI writes D47's replies. Every provider except None receives your question, D47's reply so
far, the persona and the game state D47 assembled from your journal on each turn the model
answers. Journal files are never uploaded.

| Provider | Cost | Key | Runs on this computer | What leaves the machine |
| --- | --- | --- | --- | --- |
| None (local only) | Free | None | Yes | Nothing. |
| [Anthropic](https://www.anthropic.com/pricing) | Pay per use | Required | No | The turn text and game state, to Anthropic. |
| [OpenAI](https://openai.com/api/pricing/) | Pay per use | Required | No | The turn text and game state, to OpenAI. |
| OpenAI-compatible endpoint | Set by the server | Optional | If the server is on this computer | The turn text and game state, to the address you set. Nothing leaves if that is this computer. |

- **None (local only)** suits a Commander who wants nothing sent. D47 answers what it understands
  and says so when it cannot.
- **Anthropic** is the default and suits a Commander who wants Claude's answers.
- **OpenAI** suits a Commander who wants one key to cover conversation, voice and listening.
- **OpenAI-compatible endpoint** suits a Commander running Ollama or LM Studio, or a gateway they
  trust. Set its address in Settings.

## Voice {#voice}

How D47 sounds. The text of each spoken line goes to the provider, except for the local voices.
That includes re-voiced in-game messages, which other players write.

| Provider | Cost | Key | Runs on this computer | What leaves the machine |
| --- | --- | --- | --- | --- |
| Edge Neural | Free | None | No | Spoken text, to Microsoft's Edge Read Aloud service. |
| Kokoro | Free | None | Yes | Nothing, after a one-off model download. |
| Chatterbox | Free | None | Yes, on the processor | Nothing, after a one-off model download. |
| [ElevenLabs](https://elevenlabs.io/pricing/api) | Pay per use | Required | No | Spoken text and the key, to ElevenLabs. |
| [OpenAI](https://openai.com/api/pricing/) | Pay per use | Required | No | Spoken text and the key, to OpenAI. |
| [Cartesia](https://cartesia.ai/pricing) | Pay per use | Required | No | Spoken text and the key, to Cartesia. |
| None | Free | None | Yes | Nothing. Replies stay as text. |

- **Edge Neural** is the default and suits a Commander who wants a voice with no account.
- **Kokoro** suits a Commander who wants nothing sent and a small download.
- **Chatterbox** suits a Commander who wants nothing sent and accepts a larger download.
- **ElevenLabs** suits a Commander who wants the widest choice of voices.
- **OpenAI** and **Cartesia** suit a Commander who already holds that provider's key.
- **None** suits a Commander who reads replies.

## Listening {#listening}

How your speech becomes text. A hosted provider receives the audio of what you say while the talk
button is held, and nothing is sent until a key is stored.

| Provider | Cost | Key | Runs on this computer | What leaves the machine |
| --- | --- | --- | --- | --- |
| This computer | Free | None | Yes | Nothing. Whisper runs here. |
| [Groq](https://groq.com/pricing) | Pay per use | Required | No | Your speech audio, to Groq. |
| [OpenAI](https://openai.com/api/pricing/) | Pay per use | Required | No | Your speech audio, to OpenAI. |
| [Deepgram](https://deepgram.com/pricing) | Pay per use | Required | No | Your speech audio, to Deepgram. |
| [ElevenLabs](https://elevenlabs.io/pricing/api) | Pay per use | Required | No | Your speech audio, to ElevenLabs. |

- **This computer** is the default and suits a Commander who wants audio to stay here.
- **Groq** suits a Commander whose computer is slow at Whisper.
- **OpenAI** and **ElevenLabs** suit a Commander who already holds that key for conversation or
  voice.
- **Deepgram** suits a Commander who wants a separate transcription service.
