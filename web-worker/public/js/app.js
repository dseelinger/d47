// Loads the snapshot, keeps it fresh and draws it. The decisions live in views.js; this file owns
// the fetch calls, the timers and the page root.

import { screen } from './dom.js';
import { parseRoute, shouldRefresh, stateOf, themeOf } from './views.js';

const root = document.getElementById('app');
const TICK_MS = 15 * 1000;

let state = null;
let lastAsked = null;
let menuOpen = false;
let confirming = false;
let appliedVars = [];

async function ask() {
  lastAsked = Date.now();
  try {
    const response = await fetch('/snapshot', { credentials: 'same-origin', cache: 'no-store' });
    const body = response.status === 200 || response.status === 404 ? await response.json().catch(() => null) : null;
    state = stateOf(response.status, body);
  } catch {
    state = state?.kind === 'snapshot' ? state : { kind: 'error', status: 0 };
  }
  paint(true);
}

async function leave(request) {
  try {
    await fetch(request.url, { method: request.method, credentials: 'same-origin' });
  } catch {
    // The next request for the snapshot says whether the session is still there.
  }
  confirming = false;
  menuOpen = false;
  location.hash = '';
  await ask();
}

const ui = {
  menuOpen: false,
  confirming: false,
  signOut: () => leave({ url: '/signout', method: 'POST' }),
  askDelete: () => {
    confirming = true;
    paint(true);
  },
  keep: () => {
    confirming = false;
    paint(true);
  },
  confirmDelete: () => leave({ url: '/account', method: 'DELETE' }),
  setMenu: (open) => {
    menuOpen = open;
    paint(true);
  },
  closeMenu: () => {
    menuOpen = false;
  },
};

function applyTheme(theme) {
  root.setAttribute('data-d47-theme', theme.id);
  for (const name of appliedVars) root.style.removeProperty(name);
  appliedVars = Object.keys(theme.vars);
  for (const [name, value] of Object.entries(theme.vars)) root.style.setProperty(name, value);
}

let drawn = '';

function paint(force = false) {
  if (!state) return;
  const now = Date.now();
  applyTheme(themeOf(state));
  const nodes = screen(state, parseRoute(location.hash), now, { ...ui, menuOpen, confirming });

  const holder = document.createElement('div');
  holder.append(...nodes);
  const signature = `${holder.textContent}|${menuOpen}|${confirming}|${location.hash}`;
  if (!force && signature === drawn) return;
  drawn = signature;

  root.replaceChildren(...holder.childNodes);
  if (force) window.scrollTo({ top: window.scrollY });
}

window.addEventListener('hashchange', () => {
  menuOpen = false;
  paint(true);
  window.scrollTo(0, 0);
});

function tick() {
  if (document.hidden) return;
  if (shouldRefresh({ now: Date.now(), lastAsked, hidden: false, signedOut: state?.kind === 'signedOut' })) {
    ask();
  } else {
    paint(false);
  }
}

document.addEventListener('visibilitychange', tick);
setInterval(tick, TICK_MS);
ask();
