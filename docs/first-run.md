---
title: First run
group: General help
nav_order: 2
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published rather than locally.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Five short choices. Skip any of them and D47 still starts.</p>
<section>
<h2><span class="num">1</span> Pick an AI, a voice and an ear.</h2>
<svg viewBox="0 0 880 170" role="img" aria-label="Three choices: conversation, voice and listening">
 <rect x="30" y="30" width="240" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="150" y="68" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">CONVERSATION</text>
 <text x="150" y="94" text-anchor="middle" font-size="14" fill="var(--text-muted)">who writes the replies</text>
 <rect x="320" y="30" width="240" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="68" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">VOICE</text>
 <text x="440" y="94" text-anchor="middle" font-size="14" fill="var(--text-muted)">how D47 sounds</text>
 <rect x="610" y="30" width="240" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="730" y="68" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">LISTENING</text>
 <text x="730" y="94" text-anchor="middle" font-size="14" fill="var(--text-muted)">how it hears you</text>
 <text x="440" y="154" text-anchor="middle" font-size="14" fill="var(--text-muted)">Each one can run on your computer or on a service.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Keys only for services.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="A key is asked for only when a chosen provider needs one">
 <rect x="30" y="30" width="360" height="80" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="210" y="66" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">A service you chose</text>
 <text x="210" y="90" text-anchor="middle" font-size="14" fill="var(--text-muted)">asks for a key</text>
 <rect x="490" y="30" width="360" height="80" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="670" y="66" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">Everything on your computer</text>
 <text x="670" y="90" text-anchor="middle" font-size="14" fill="var(--text-muted)">asks for nothing</text>
</svg>
</section>
</div></div>

## The steps

Setup opens by itself on a first launch, and again when the conversation provider needs a key that
is not stored. **SKIP SETUP** and Esc close it with every setting unchanged. Nothing is saved until
**START** on the last step. The seven steps, in order:

1. **Welcome.** Lists the five choices. **BEGIN** starts them. Nothing is chosen here.
2. **Conversation.** Which AI writes D47's replies. The default is Anthropic. Choosing *None (local
   only)* leaves D47 answering only what it understands without a model. The providers are compared
   under [Conversation](choosing-providers.html#conversation).
3. **Voice.** How D47 speaks. The default is Edge Neural, which needs no account. *None* leaves
   replies as text. See [Voice](choosing-providers.html#voice).
4. **Listening.** How your speech becomes text. The default is This computer, running Whisper, so
   audio stays here. See [Listening](choosing-providers.html#listening).
5. **Keys.** Appears only when a chosen provider needs a key. Each key is asked for once, however
   many choices it covers. Skip a key and that part uses the free choice until you add one in
   Settings: no AI for conversation, Edge Neural for voice, This computer for listening.
6. **Talk button.** The button you hold to talk. The default is the Scroll Lock key, held to talk.
   A button on a stick or throttle is easier to reach in flight. Setup says when Elite already
   binds the same button or key to something else.
7. **Ready.** Shows each choice and what it sends. Select one to change it. **START** saves.

Everything setup chooses can be changed later in Settings. What each choice sends is listed under
**Privacy and egress** in Settings.

## Keys

A key lets D47 use your account with a provider. D47 stores it encrypted for your Windows user and
sends it only to that provider. Each provider issues its own, from the page linked below, and bills
you directly. D47 quotes no prices; [Choosing providers](choosing-providers.html) links each
provider's pricing page.

| Provider | Get a key | Choices that key also covers |
| --- | --- | --- |
| Anthropic | [console.anthropic.com](https://console.anthropic.com/settings/keys) | Conversation only. |
| OpenAI | [platform.openai.com](https://platform.openai.com/api-keys) | Conversation, voice and listening. |
| ElevenLabs | [elevenlabs.io](https://elevenlabs.io/app/settings/api-keys) | Voice and listening. |
| Cartesia | [play.cartesia.ai](https://play.cartesia.ai/keys) | Voice only. |
| Groq | [console.groq.com](https://console.groq.com/keys) | Listening only. |
| Deepgram | [console.deepgram.com](https://console.deepgram.com/) | Listening only. |

The *OpenAI-compatible endpoint* conversation choice has a key box that can stay empty: a server on
your own computer usually needs none. Setup has no key page for it.
