// What each page draws, worked out from the snapshot document without touching the DOM.
// Every function takes the document and an explicit `now`, and returns strings, numbers and class
// names. dom.js turns them into text nodes; nothing here produces markup.

export const STALE_DAYS = 30;
export const REFRESH_MS = 60 * 1000;
export const MOST_WATCHES = 32;

const HOUR = 60 * 60 * 1000;
const DAY = 24 * HOUR;
const MONTHS = ['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC'];

export const THEMES = ['elite', 'dark', 'light', 'elite-palette'];

const array = (value) => (Array.isArray(value) ? value : []);
const object = (value) => (value && typeof value === 'object' && !Array.isArray(value) ? value : null);
const text = (value) => (typeof value === 'string' && value.trim() !== '' ? value : null);
const finite = (value) => (typeof value === 'number' && Number.isFinite(value) ? value : null);
const clamp = (value, low, high) => Math.min(high, Math.max(low, value));
const pad = (n) => String(n).padStart(2, '0');

const grouped = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const oneDecimal = new Intl.NumberFormat('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const twoDecimals = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export const number = (value) => (finite(value) === null ? '—' : grouped.format(value));
export const credits = (value) => (finite(value) === null ? '—' : `${grouped.format(value)} CR`);
export const lightYears = (value) => (finite(value) === null ? '—' : `${twoDecimals.format(value)} LY`);
export const wholeLightYears = (value) => (finite(value) === null ? '—' : `${grouped.format(value)} LY`);
export const tonnes = (value, digits = 0) =>
  finite(value) === null ? '—' : `${digits === 1 ? oneDecimal.format(value) : grouped.format(value)} T`;
export const plural = (count, one, many = `${one}s`) => `${grouped.format(count)} ${count === 1 ? one : many}`;
export const percent = (value) => (finite(value) === null ? '—' : `${grouped.format(value)}%`);

export function parseTime(value) {
  if (typeof value !== 'string' && typeof value !== 'number') return null;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

export const clock = (date) => `${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())} UTC`;
export const dayMonth = (date) => `${date.getUTCDate()} ${MONTHS[date.getUTCMonth()]}`;
export const dayMonthWord = (date) => {
  const month = MONTHS[date.getUTCMonth()];
  return `${date.getUTCDate()} ${month[0]}${month.slice(1).toLowerCase()}`;
};
const sameUtcDay = (a, b) => a.toISOString().slice(0, 10) === b.toISOString().slice(0, 10);

export function ago(then, now) {
  const elapsed = Math.max(0, now - then);
  if (elapsed < 90 * 1000) return 'just now';
  const minutes = Math.round(elapsed / 60000);
  if (minutes < 60) return `${plural(minutes, 'minute')} ago`;
  const hours = Math.round(elapsed / HOUR);
  if (hours < 48) return `${plural(hours, 'hour')} ago`;
  return `${plural(Math.floor(elapsed / DAY), 'day')} ago`;
}

// ---------------------------------------------------------------------------------------------
// States and the request that decides them

/** Turns the answer to GET /snapshot into the state the page draws. */
export function stateOf(status, body) {
  if (status === 200) {
    const doc = object(body);
    return doc ? { kind: 'snapshot', doc } : { kind: 'none' };
  }
  if (status === 401) return { kind: 'signedOut' };
  if (status === 404) {
    const named = object(body)?.state;
    if (named === 'linked') return { kind: 'linked' };
    if (named === 'off') return { kind: 'off', since: parseTime(object(body)?.since) };
    return { kind: 'none' };
  }
  return { kind: 'error', status };
}

/** The snapshot is asked for again only while the page is visible, and no more than once a minute. */
export function shouldRefresh({ now, lastAsked, hidden, signedOut = false }) {
  if (hidden || signedOut) return false;
  return lastAsked === null || lastAsked === undefined || now - lastAsked >= REFRESH_MS;
}

// ---------------------------------------------------------------------------------------------
// Theme

const ELITE = {
  '--d47-a': [0xff, 0x7a, 0x1a],
  '--d47-knock': [0x14, 0x08, 0x00],
  '--d47-brown': [0x6b, 0x2f, 0x00],
  '--d47-cyan': [0x33, 0xd6, 0xe8],
  '--d47-blue': [0x1f, 0xa8, 0xf5],
  '--d47-red': [0xf0, 0x34, 0x3f],
  '--d47-warn': [0xff, 0xa4, 0x1c],
  '--d47-yellow': [0xf5, 0xd4, 0x26],
};

const hex = (n) => pad(n.toString(16));

/** Elite's HUD colour matrix applied to a colour, as GuiColourMatrix.Transform does. */
export function transformColour(matrix, [r, g, b]) {
  const [rr, rg, rb, gr, gg, gb, br, bg, bb] = matrix;
  const channel = (x, y, z) => clamp(Math.round(((r / 255) * x + (g / 255) * y + (b / 255) * z) * 255), 0, 255);
  return [channel(rr, rg, rb), channel(gr, gg, gb), channel(br, bg, bb)];
}

/**
 * The theme a state draws in: the snapshot's own, Elite for a page with no snapshot or an id this
 * page does not know. For My HUD colours, `vars` recolours the coloured tokens only.
 */
export function themeOf(state) {
  const doc = state?.kind === 'snapshot' ? state.doc : null;
  const id = THEMES.includes(doc?.theme) ? doc.theme : 'elite';
  const matrix = array(doc?.hudMatrix);
  const usable = id === 'elite-palette' && matrix.length === 9 && matrix.every((n) => finite(n) !== null);
  const vars = {};
  if (usable) {
    for (const [name, rgb] of Object.entries(ELITE)) {
      const [r, g, b] = transformColour(matrix, rgb);
      vars[name] = `#${hex(r)}${hex(g)}${hex(b)}`;
    }
  }
  return { id, vars };
}

// ---------------------------------------------------------------------------------------------
// Bar

/** The snapshot's age as the bar and the phone's age line draw it. */
export function ageView(takenAt, now) {
  const when = parseTime(takenAt);
  if (!when) return null;
  const elapsed = now - when.getTime();
  const old = elapsed > STALE_DAYS * DAY;
  const days = Math.floor(elapsed / DAY);
  const stamp = sameUtcDay(when, new Date(now)) ? clock(when) : `${dayMonth(when)} · ${clock(when)}`;
  const relative = ago(when.getTime(), now);

  return {
    old,
    top: old ? `SNAPSHOT ${dayMonth(when)} · ${days} ${days === 1 ? 'DAY' : 'DAYS'} AGO` : `SNAPSHOT ${stamp}`,
    bottom: old ? 'OLD SNAPSHOT' : relative,
    line: old
      ? `Snapshot ${dayMonthWord(when)} · ${plural(days, 'day')} ago · OLD SNAPSHOT`
      : `Snapshot ${stamp} · ${relative}`,
    taken: `${dayMonth(when)} · ${clock(when)}`,
  };
}

export function whoOf(state) {
  if (state?.kind === 'snapshot') {
    const name = text(state.doc.name);
    return name ? `CMDR ${name.toUpperCase()}` : 'FRONTIER ACCOUNT';
  }
  return state?.kind === 'signedOut' || !state ? '' : 'FRONTIER ACCOUNT';
}

// ---------------------------------------------------------------------------------------------
// Navigation

export const TABS = [
  {
    id: 'commander',
    label: 'Commander',
    home: 'progress',
    pages: ['checklist', 'watches', 'missions', 'progress'],
    groupLabel: 'Commander pages',
  },
  { id: 'assets', label: 'Asset mgmt', home: 'ships', pages: ['ships', 'carrier', 'engineers', 'materials'], groupLabel: 'Asset pages' },
  { id: 'navigation', label: 'Navigation', home: 'plan', pages: ['plan'], groupLabel: 'Navigation pages' },
];

export const PAGES = {
  progress: { label: 'Progress', title: 'Commander' },
  checklist: { label: 'Checklist', title: 'Commander' },
  watches: { label: 'Watches', title: 'Commander' },
  missions: { label: 'Missions', title: 'Commander' },
  ships: { label: 'Ships', title: 'Asset mgmt' },
  carrier: { label: 'Carrier', title: 'Asset mgmt' },
  engineers: { label: 'Engineers', title: 'Asset mgmt' },
  materials: { label: 'Materials', title: 'Asset mgmt' },
  plan: { label: 'Plan', title: 'Navigation' },
  account: { label: 'Account', title: 'Account' },
};

export const tabOf = (page) => TABS.find((tab) => tab.pages.includes(page)) ?? null;

/** `#/ships/2` is the Ships page with its third entry chosen. Anything unknown is Progress. */
export function parseRoute(hash) {
  const [page, argument] = String(hash ?? '').replace(/^#\/?/, '').split('/');
  return { page: Object.hasOwn(PAGES, page) ? page : 'progress', argument: argument ? decodeURIComponent(argument) : null };
}

export const routeHref = (page, argument = null) =>
  `#/${page}${argument === null || argument === undefined ? '' : `/${encodeURIComponent(argument)}`}`;

// ---------------------------------------------------------------------------------------------
// Progress

const LADDERS = {
  Combat: ['Harmless', 'Mostly Harmless', 'Novice', 'Competent', 'Expert', 'Master', 'Dangerous', 'Deadly'],
  Trade: ['Penniless', 'Mostly Penniless', 'Peddler', 'Dealer', 'Merchant', 'Broker', 'Entrepreneur', 'Tycoon'],
  Explore: ['Aimless', 'Mostly Aimless', 'Scout', 'Surveyor', 'Trailblazer', 'Pathfinder', 'Ranger', 'Pioneer'],
  Soldier: ['Defenceless', 'Mostly Defenceless', 'Rookie', 'Soldier', 'Gunslinger', 'Warrior', 'Gladiator', 'Deadeye'],
  Exobiologist: ['Directionless', 'Mostly Directionless', 'Compiler', 'Collector', 'Cataloguer', 'Taxonomist', 'Ecologist', 'Geneticist'],
  CQC: ['Helpless', 'Mostly Helpless', 'Amateur', 'Semi Professional', 'Professional', 'Champion', 'Hero', 'Legend'],
};

const EMPIRE = [
  'None', 'Outsider', 'Serf', 'Master', 'Squire', 'Knight', 'Lord', 'Baron',
  'Viscount', 'Count', 'Earl', 'Marquis', 'Duke', 'Prince', 'King',
];
const FEDERATION = [
  'None', 'Recruit', 'Cadet', 'Midshipman', 'Petty Officer', 'Chief Petty Officer',
  'Warrant Officer', 'Ensign', 'Lieutenant', 'Lieutenant Commander', 'Post Commander',
  'Post Captain', 'Rear Admiral', 'Vice Admiral', 'Admiral',
];

const CAREER_LABELS = { Combat: 'Combat', Trade: 'Trade', Explore: 'Explore', Soldier: 'Mercenary', Exobiologist: 'Exobiologist', CQC: 'CQC' };
const NAVY_LABELS = { Empire: 'Imperial navy', Federation: 'Federal navy' };
const ELITE_GRADES = ['', 'I', 'II', 'III', 'IV', 'V'];
const CAREER_TOP = 13;
const NAVY_TOP = 14;

export function careerRankName(career, rank) {
  if (!Number.isInteger(rank) || rank < 0) return null;
  if (rank === 8) return 'Elite';
  if (rank > 8 && rank <= CAREER_TOP) return `Elite ${ELITE_GRADES[rank - 8]}`;
  return LADDERS[career]?.[rank] ?? null;
}

const rankCard = (label, name, rank, top, percentOfRank) => ({
  career: label,
  rank: (name ?? `RANK ${rank}`).toUpperCase(),
  meta: `RANK ${rank} OF ${top}${finite(percentOfRank) === null ? '' : ` · ${percentOfRank}%`}`,
  pct: clamp(finite(percentOfRank) ?? 0, 0, 100),
});

export function ranksView(progress) {
  const careers = array(progress?.ranks)
    .filter((r) => text(r?.career) && Number.isInteger(r.rank))
    .map((r) => ({ order: Object.keys(CAREER_LABELS).indexOf(r.career), r }))
    .sort((a, b) => (a.order < 0 ? 99 : a.order) - (b.order < 0 ? 99 : b.order))
    .map(({ r }) => rankCard(CAREER_LABELS[r.career] ?? r.career, careerRankName(r.career, r.rank), r.rank, CAREER_TOP, r.percent));

  const navies = array(progress?.navy)
    .filter((r) => text(r?.career) && Number.isInteger(r.rank))
    .map((r) => {
      const ladder = r.career === 'Empire' ? EMPIRE : r.career === 'Federation' ? FEDERATION : [];
      return rankCard(NAVY_LABELS[r.career] ?? r.career, ladder[r.rank] ?? null, r.rank, NAVY_TOP, r.percent);
    });

  return { careers, navies };
}

const BANDS = [
  ['HOSTILE', -100],
  ['UNFRIENDLY', -90],
  ['NEUTRAL', -35],
  ['CORDIAL', 4],
  ['FRIENDLY', 35],
  ['ALLIED', 90],
];
const POWER_ORDER = ['Empire', 'Federation', 'Alliance', 'Independent'];

export function reputationBand(value) {
  return BANDS.reduce((word, [name, floor]) => (value >= floor ? name : word), 'HOSTILE');
}

export function reputationView(progress) {
  return array(progress?.reputation)
    .filter((r) => text(r?.superpower) && finite(r.reputation) !== null)
    .sort((a, b) => POWER_ORDER.indexOf(a.superpower) - POWER_ORDER.indexOf(b.superpower))
    .map((r) => {
      const rounded = Math.round(r.reputation);
      return {
        power: r.superpower === 'Independent' ? 'Independents' : r.superpower,
        word: reputationBand(r.reputation),
        num: `${rounded > 0 ? '+' : rounded < 0 ? '−' : ''}${Math.abs(rounded)}`,
        pct: clamp((r.reputation + 100) / 2, 0, 100),
      };
    });
}

export function progressView(doc) {
  const progress = object(doc.progress);
  if (!progress) return null;
  const location = object(progress.location);
  const power = [text(location?.powerplayState), text(location?.controllingPower)].filter(Boolean).join(' · ');
  const tiles = [
    location && text(location.starSystem) && { label: 'Current system', value: location.starSystem, tone: 'cyan' },
    text(location?.stationName) && { label: 'Docked at', value: location.stationName, tone: 'white' },
    text(location?.mode) && { label: 'Flight', value: location.mode, tone: '' },
    text(location?.body) && { label: 'Body', value: location.body, tone: 'white small' },
    text(location?.systemFaction) && { label: 'Controlling faction', value: location.systemFaction, tone: 'white small' },
    power && { label: 'Powerplay', value: power, tone: 'small' },
  ].filter(Boolean);

  return {
    name: whoOf({ kind: 'snapshot', doc }),
    balance: finite(progress.balance) === null ? null : credits(progress.balance),
    tiles,
    ...ranksView(progress),
    reputation: reputationView(progress),
  };
}

// ---------------------------------------------------------------------------------------------
// Checklist

const HOMES = [
  ['Yours', 'Yours'],
  ['Ships', 'Ships'],
  ['Systems', 'Systems'],
  ['SuitsAndWeapons', 'Suits and weapons'],
];

const ITEM_STATES = {
  Done: { cls: 'met', label: '✓ Done' },
  Open: { cls: '', label: 'Open' },
  Elsewhere: { cls: 'unmet', label: 'Elsewhere' },
  Blocked: { cls: 'blocked', label: 'Blocked' },
  Unverified: { cls: 'unknown', label: '? Unverified' },
};

export function checklistView(doc, argument) {
  const checklist = object(doc.checklist);
  if (!checklist) return null;

  const lists = array(checklist.lists).filter((list) => text(list?.name));
  const chosen = Number.isInteger(Number(argument)) && argument !== null && lists[Number(argument)]
    ? Number(argument)
    : Math.max(0, lists.findIndex((list) => list.isHere));

  const groups = HOMES.map(([home, title]) => ({
    title,
    rows: lists
      .map((list, index) => ({ list, index }))
      .filter(({ list }) => list.home === home)
      .map(({ list, index }) => ({
        index,
        name: list.name,
        selected: index === chosen,
        sub: list.isHere
          ? home === 'Ships' ? 'Flying now' : 'You are here'
          : text(list.next) ?? (list.open === 0 ? 'Every line done' : ''),
        aside: [list.open > 0 && `${number(list.open)} open`, list.done > 0 && `${number(list.done)} done`].filter(Boolean).join(' · '),
      })),
  })).filter((group) => group.rows.length > 0);

  const shown = lists[chosen] ?? null;
  const homeTitle = HOMES.find(([home]) => home === shown?.home)?.[1] ?? '';

  const goals = array(checklist.goals)
    .filter((goal) => text(goal?.name))
    .map((goal) => {
      const pct = goal.isDone ? 100 : clamp(Math.round((finite(goal.fraction) ?? 0) * 100), 0, 100);
      return {
        goal: goal.name,
        line: goal.isDone ? '✓ MET' : text(goal.nextRank) ? `NEXT: ${goal.nextRank}`.toUpperCase() : '',
        pct,
        figures: finite(goal.have) !== null && finite(goal.need) !== null ? `${number(goal.have)} OF ${number(goal.need)}` : '',
      };
    });

  return {
    goals,
    groups,
    detail: shown && {
      context: shown.isHere && shown.home === 'Ships' ? `${homeTitle} · flying now` : homeTitle,
      title: shown.name,
      lines: array(shown.items).map((item) => {
        const state = ITEM_STATES[item?.state] ?? ITEM_STATES.Open;
        return { cls: state.cls, state: state.label, text: String(item?.text ?? '') };
      }),
    },
    openLines: lists.reduce((sum, list) => sum + (Number.isInteger(list.open) ? list.open : 0), 0),
    listCount: lists.length,
  };
}

// ---------------------------------------------------------------------------------------------
// Watches

const WATCH_KINDS = [
  ['Stock', 'Stock'],
  ['NewCommunityGoal', 'Community Goal'],
  ['NextSystem', 'Next system with'],
];

export function watchesView(doc, argument) {
  const watches = array(doc.watches).filter((w) => text(w?.name));
  if (!object(doc) || !Array.isArray(doc.watches)) return null;

  const kinds = WATCH_KINDS.map(([kind, label]) => {
    const own = watches.filter((w) => w.kind === kind);
    return { kind, label, count: own.length, status: plural(own.length, 'watch', 'watches') };
  });
  const chosen = kinds.find((k) => k.kind === argument) ?? kinds.find((k) => k.count > 0) ?? kinds[0];

  const rows = watches
    .filter((w) => w.kind === chosen.kind)
    .map((w) => {
      let sub;
      if (chosen.kind === 'Stock') {
        const where = [text(w.station), text(w.system)].filter(Boolean).join(', ');
        const limit = text(w.comparison) && finite(w.threshold) !== null ? `${w.comparison} ${number(w.threshold)}` : null;
        sub = [limit, where].filter(Boolean).join(' · ');
      } else if (chosen.kind === 'NextSystem') {
        sub = [text(w.service), text(w.serviceKind), text(w.minPad) && `${w.minPad} pad or larger`].filter(Boolean).join(' · ');
      } else {
        sub = [text(w.system), text(w.station)].filter(Boolean).join(', ');
      }

      if (w.enabled === false) return { name: w.name, sub, aside: 'Off', tone: 'off' };
      if (text(w.fired)) return { name: w.name, sub, aside: w.fired, tone: 'fired' };
      return { name: w.name, sub, aside: 'Watching', tone: '' };
    });

  return { kinds, chosen: chosen.kind, title: chosen.label, rows, total: watches.length };
}

// ---------------------------------------------------------------------------------------------
// Missions

/** Time left as the Missions page words it, counted from the viewer's clock. */
export function timeLeft(expiry, now) {
  const when = parseTime(expiry);
  if (!when) return { text: '—', expired: false, ms: null };
  const ms = when.getTime() - now;
  if (ms <= 0) return { text: 'Expired', expired: true, ms };
  if (ms < HOUR) {
    return { text: `0:${pad(Math.floor(ms / 60000))}`, expired: false, ms };
  }
  if (ms < DAY) return { text: `${Math.floor(ms / HOUR)}H`, expired: false, ms };
  return { text: `${Math.floor(ms / DAY)}D ${Math.floor((ms % DAY) / HOUR)}H`, expired: false, ms };
}

/** Which band a mission falls in at `now`: within the hour first, then here, then the rest. */
export function missionBand(mission, location, now) {
  const left = timeLeft(mission.expiry, now);
  if (left.ms !== null && left.ms < HOUR) return 'hour';
  const here = text(location?.starSystem);
  const station = text(location?.stationName);
  if (here && mission.destinationSystem === here && (!text(mission.destinationStation) || !station || mission.destinationStation === station)) {
    return 'here';
  }
  return 'rest';
}

const BAND_TITLES = { hour: 'Expiring within the hour', here: 'Hand in here', rest: 'Everything else' };

export function missionsView(doc, now) {
  if (!Array.isArray(doc.missions)) return null;
  const location = object(object(doc.progress)?.location);
  const missions = array(doc.missions).filter((m) => text(m?.title));

  const rows = missions.map((m) => {
    const left = timeLeft(m.expiry, now);
    const cargo = object(m.cargo);
    const extras = [
      text(m.targetFaction) && `Against ${m.targetFaction}`,
      cargo && cargo.total > 0 && (cargo.delivered > 0
        ? `Cargo ${number(cargo.delivered)} of ${number(cargo.total)} delivered`
        : `Cargo ${number(cargo.collected)} of ${number(cargo.total)} collected`),
      Number.isInteger(m.passengerCount) && m.passengerCount > 0 && `Passengers ${number(m.passengerCount)}`,
    ].filter(Boolean);

    return {
      band: missionBand(m, location, now),
      ms: left.ms ?? Number.POSITIVE_INFINITY,
      title: m.title,
      giver: text(m.faction) ?? '',
      station: text(m.destinationStation) ?? '',
      system: text(m.destinationSystem) ?? '',
      extra: extras.join(' · '),
      left: left.text,
      urgent: left.expired || (left.ms !== null && left.ms < HOUR),
      reward: finite(m.reward) === null ? '—' : credits(m.reward),
    };
  });

  const bands = ['hour', 'here', 'rest']
    .map((band) => {
      const own = rows.filter((r) => r.band === band).sort((a, b) => a.ms - b.ms);
      return { band, title: BAND_TITLES[band], count: own.length, rows: own };
    })
    .filter((band) => band.count > 0);

  return {
    bands,
    count: missions.length,
    total: missions.reduce((sum, m) => sum + (finite(m.reward) ?? 0), 0),
  };
}

// ---------------------------------------------------------------------------------------------
// Ships

function whereWords(ship) {
  const station = text(ship.stationName);
  const system = text(ship.starSystem);
  if (ship.inTransit) return system ? `In transit to ${system}.` : 'In transit.';
  if (station && system) return `Parked at ${station}, ${system}.`;
  if (system) return `Parked in ${system}.`;
  return 'Location unknown.';
}

const shipLabel = (type, name) => (text(name) ? `${name} (${type})` : type);

export function shipsView(doc) {
  const ships = object(doc.ships);
  if (!ships) return null;

  const flown = object(ships.flown);
  const location = text(object(object(doc.progress)?.location)?.starSystem);
  const stored = array(ships.fleet)
    .filter((s) => text(s?.type))
    .map((s) => ({ label: shipLabel(s.type, s.name), where: whereWords(s), value: finite(s.value) === null ? '' : credits(s.value) }));

  const flownName = text(flown?.name) ?? text(flown?.typeName);
  const flownLabel = flown && flownName
    ? text(flown.name) && text(flown.typeName) ? `${flown.name} (${flown.typeName})` : flownName
    : null;

  const detail = flown && {
    context: [text(flown.typeName), text(flown.ident)].filter(Boolean).join(' · '),
    title: flownName ?? 'Ship',
    rebuy: finite(flown.rebuy) === null ? null : credits(flown.rebuy),
    tiles: [
      finite(flown.maxJumpRange) !== null && { label: 'Jump range', value: lightYears(flown.maxJumpRange) },
      finite(flown.cargoCapacity) !== null && { label: 'Hold', value: tonnes(flown.cargoCapacity) },
      finite(flown.fuelCapacity) !== null && { label: 'Fuel', value: tonnes(flown.fuelCapacity) },
      finite(flown.unladenMass) !== null && { label: 'Unladen', value: tonnes(flown.unladenMass, 1) },
      finite(flown.totalValue) !== null && { label: 'Value with modules', value: credits(flown.totalValue) },
      finite(flown.hullHealth) !== null && { label: 'Hull integrity', value: percent(flown.hullHealth) },
    ].filter(Boolean),
    modules: array(flown.modules)
      .filter((m) => text(m?.item))
      .map((m) => ({
        item: m.item,
        slot: text(m.slot) ?? '',
        engineering: text(m.blueprint)
          ? `${m.blueprint}${Number.isInteger(m.blueprintLevel) ? ` · G${m.blueprintLevel}` : ''}`
          : 'Not engineered',
        engineered: Boolean(text(m.blueprint)),
        experimental: text(m.experimental) ?? '—',
        health: finite(m.health) === null ? '—' : percent(m.health),
      })),
  };

  const count = stored.length + (flown ? 1 : 0);
  return {
    flownLabel,
    flownWhere: location ? `You are flying it, in ${location}.` : 'You are flying it.',
    stored,
    detail,
    summary: `${plural(count, 'ship')}.${flownName ? ` Flying ${flownName}${location ? ` in ${location}` : ''}.` : ''}`,
  };
}

// ---------------------------------------------------------------------------------------------
// Carrier

export function carrierView(doc, now) {
  const carrier = object(doc.carrier);
  if (!carrier) return null;

  const capacity = finite(carrier.capacity);
  const free = finite(carrier.freeSpace);
  const used = capacity !== null && free !== null ? Math.max(0, capacity - free) : null;
  const upkeep = finite(carrier.weeklyUpkeep);
  const balance = finite(carrier.balance);
  const departure = parseTime(carrier.departureTime);
  const range = finite(carrier.jumpRange) ?? finite(carrier.maxJumpRange);
  const tank = finite(carrier.fuelLevel);
  const hold = finite(carrier.tritiumInHold);

  const services = array(carrier.services).filter((s) => text(s?.role) && s.activated);
  const names = (list) => list.map((s) => s.role.toUpperCase()).join(' · ');

  return {
    context: ['Fleet carrier', text(carrier.callSign)].filter(Boolean).join(' · '),
    title: text(carrier.name) ?? text(carrier.callSign) ?? 'Fleet carrier',
    balance: balance === null ? null : credits(balance),
    tiles: [
      text(carrier.starSystem) && { label: 'System', value: carrier.starSystem },
      text(carrier.destinationSystem) && {
        label: 'Jumping to',
        value: carrier.destinationSystem,
        note: departure && departure.getTime() > now ? `DEPARTS ${clock(departure)}` : null,
      },
      range !== null && { label: 'Jump range', value: wholeLightYears(range), mono: true },
      used !== null && capacity > 0 && {
        label: 'Space',
        value: `${number(used)} / ${number(capacity)} T`,
        gauge: clamp((used / capacity) * 100, 0, 100),
      },
      finite(carrier.cargoTonnes) !== null && { label: 'Cargo', value: tonnes(carrier.cargoTonnes), mono: true },
      text(carrier.dockingAccess) && { label: 'Docking', value: carrier.dockingAccess.toUpperCase() },
      upkeep !== null && { label: 'Upkeep', value: `${number(upkeep)} CR A WEEK`, mono: true },
      upkeep !== null && upkeep > 0 && balance !== null && {
        label: 'Covers',
        value: `${number(Math.floor(balance / upkeep))} WEEKS`,
        mono: true,
      },
    ].filter(Boolean),
    tritium: [
      tank !== null && { label: 'In the tank', value: `${number(tank)} / 1,000 T`, gauge: clamp(tank / 10, 0, 100) },
      hold !== null && { label: 'In the hold', value: tonnes(hold), mono: true },
      (tank !== null || hold !== null) && { label: 'Total', value: tonnes((tank ?? 0) + (hold ?? 0)), mono: true },
    ].filter(Boolean),
    open: names(services.filter((s) => s.enabled)),
    off: names(services.filter((s) => !s.enabled)),
  };
}

// ---------------------------------------------------------------------------------------------
// Engineers

const REACH_GROUPS = [
  ['WithinReach', 'Ready for unlock'],
  ['Unlocked', 'Unlocked'],
  ['Locked', 'Needs a referral'],
];

const distance = (e) =>
  finite(e.lightYears) === null
    ? ''
    : `${number(e.lightYears)} LY${Number.isInteger(e.jumps) ? ` · ~${plural(e.jumps, 'jump')}` : ''}`;

function standingWords(e) {
  const standing = object(e.standing);
  if (e.reach === 'Unlocked') {
    if (standing && Number.isInteger(standing.rank)) {
      return `Grade ${standing.rank}${finite(standing.rankProgress) !== null && standing.rankProgress > 0 ? ` · ${standing.rankProgress}%` : ''}`;
    }
    return 'Unlocked';
  }
  if (e.reach === 'Locked') {
    const by = array(e.referredBy).filter(text);
    return by.length ? `Referred by ${by.join(', ')}` : 'Needs a referral';
  }
  if (!standing) return 'Not met';
  return standing.progress === 'Known' ? 'Known · no invitation' : String(standing.progress ?? 'Not met');
}

const PREREQUISITES = {
  Met: { cls: 'met', label: '✓ Met' },
  InProgress: { cls: '', label: 'In progress' },
  Unknown: { cls: 'unknown', label: '? Unknown' },
  NotMet: { cls: 'unmet', label: 'Not met' },
};

export function engineersView(doc, argument) {
  if (!Array.isArray(doc.engineers)) return null;
  const engineers = array(doc.engineers).filter((e) => text(e?.name));
  const location = text(object(object(doc.progress)?.location)?.starSystem);

  const sorted = (reach) =>
    engineers
      .map((e, index) => ({ e, index }))
      .filter(({ e }) => (REACH_GROUPS.some(([r]) => r === e.reach) ? e.reach === reach : reach === 'Locked'))
      .sort((a, b) => (a.e.lightYears ?? Infinity) - (b.e.lightYears ?? Infinity) || a.e.name.localeCompare(b.e.name));

  const groups = REACH_GROUPS.map(([reach, title]) => ({ reach, title, items: sorted(reach) })).filter((g) => g.items.length > 0);
  const first = groups[0]?.items[0]?.index ?? null;
  const chosen = engineers[Number(argument)] && argument !== null ? Number(argument) : first;

  const listGroups = groups.map((g) => ({
    title: g.title,
    rows: g.items.map(({ e, index }) => ({
      index,
      name: e.name,
      selected: index === chosen,
      sub: standingWords(e),
      aside: distance(e),
    })),
  }));

  const e = chosen === null ? null : engineers[chosen];
  const group = REACH_GROUPS.find(([r]) => r === e?.reach)?.[1] ?? 'Needs a referral';
  const detail = e && {
    context: group,
    title: e.name,
    tiles: [
      (text(e.station) || text(e.system)) && { label: 'Workshop', value: [e.station, e.system].filter(text).join(' · '), tone: 'small' },
      finite(e.lightYears) !== null && {
        label: location ? `Distance from ${location}` : 'Distance',
        value: distance(e).toUpperCase(),
        tone: 'small mono',
      },
      { label: 'Where you stand', value: standingWords(e).toUpperCase(), tone: 'white small' },
      array(e.specialities).length > 0 && {
        label: 'Grades',
        value: array(e.specialities)
          .filter((s) => text(s?.kind))
          .map((s) => `${s.kind}${Number.isInteger(s.grade) ? ` G${s.grade}` : ''}`)
          .join(' · ')
          .toUpperCase(),
        tone: 'small',
        wide: true,
      },
      text(e.unlockCost) && { label: 'Unlock cost', value: e.unlockCost.toUpperCase(), tone: 'small' },
    ].filter(Boolean),
    prerequisites: array(e.prerequisites)
      .filter((p) => text(p?.text))
      .map((p) => {
        const state = PREREQUISITES[p.state] ?? PREREQUISITES.Unknown;
        return {
          cls: state.cls,
          state: state.label,
          text: p.text,
          gauge: p.state === 'InProgress' && finite(p.percent) !== null ? clamp(p.percent, 0, 100) : null,
        };
      }),
  };

  const count = (reach) => engineers.filter((x) => x.reach === reach).length;
  return {
    groups: listGroups,
    detail,
    summary: `${count('Unlocked')} of ${engineers.length} unlocked. ${count('WithinReach')} ready for unlock. ${count('Locked')} need a referral.`,
  };
}

// ---------------------------------------------------------------------------------------------
// Materials

const CAPS = { 1: 300, 2: 250, 3: 200, 4: 150, 5: 100 };
const CATEGORIES = ['Raw', 'Manufactured', 'Encoded'];

export function materialsView(doc, argument) {
  if (!Array.isArray(doc.materials)) return null;
  const materials = array(doc.materials).filter((m) => text(m?.displayName) && Number.isInteger(m.count));

  const kinds = CATEGORIES.map((category) => {
    const own = materials.filter((m) => m.category === category);
    return {
      category,
      status: `${plural(own.length, 'kind')} · ${plural(own.reduce((sum, m) => sum + m.count, 0), 'unit')}`,
      count: own.length,
    };
  });
  const chosen = kinds.find((k) => k.category === argument) ?? kinds.find((k) => k.count > 0) ?? kinds[0];

  const own = materials.filter((m) => m.category === chosen.category);
  const gradeKeys = [...new Set(own.map((m) => (CAPS[m.grade] ? m.grade : 0)))].sort((a, b) => (a || 99) - (b || 99));
  const grades = gradeKeys.map((grade) => {
    const cap = CAPS[grade] ?? null;
    return {
      title: grade ? `Grade ${grade}` : 'Ungraded',
      cap: cap === null ? null : `CAP ${number(cap)} EACH`,
      items: own
        .filter((m) => (CAPS[m.grade] ? m.grade : 0) === grade)
        .sort((a, b) => a.displayName.localeCompare(b.displayName))
        .map((m) => ({
          name: m.displayName,
          count: cap === null ? number(m.count) : `${number(m.count)} / ${number(cap)}`,
          pct: cap === null ? null : clamp(Math.round((m.count / cap) * 1000) / 10, 0, 100),
        })),
    };
  });

  return { kinds, chosen: chosen.category, grades };
}

// ---------------------------------------------------------------------------------------------
// Plan

const PLAN_NAMES = {
  Jump: 'Neutron Plotter',
  Riches: 'Road to Riches',
  Trade: 'Trade Route',
  Exobiology: 'Exobiology',
  Carrier: 'Carrier Route',
  Outfitting: 'Outfitting',
};
const WAYPOINTS_SHOWN = 8;

export function planView(doc, argument) {
  if (!Array.isArray(doc.routePlans)) return null;
  const plans = array(doc.routePlans).filter((p) => text(p?.kind));
  const chosen = plans[Number(argument)] && argument !== null ? Number(argument) : 0;

  const rows = plans.map((p, index) => {
    const plotted = parseTime(p.plottedAt);
    const jump = object(p.jump);
    return {
      index,
      name: PLAN_NAMES[p.kind] ?? p.kind,
      selected: index === chosen,
      sub: text(p.headline) ?? (jump ? `${jump.origin} to ${jump.destination}` : ''),
      aside: plotted ? dayMonthWord(plotted) : '',
    };
  });

  const p = plans[chosen];
  const jump = object(p?.jump);
  let detail = null;
  if (p) {
    const plotted = parseTime(p.plottedAt);
    const stops = array(jump?.waypoints);
    const reached = Number.isInteger(p.reached) ? p.reached : -1;
    const start = clamp(reached - 1, 0, Math.max(0, stops.length - WAYPOINTS_SHOWN));
    const shown = stops.slice(start, start + WAYPOINTS_SHOWN);
    detail = {
      context: `${PLAN_NAMES[p.kind] ?? p.kind}${plotted ? ` · plotted ${dayMonthWord(plotted)}` : ''}`,
      title: jump ? `${jump.origin} to ${jump.destination}` : text(p.headline) ?? (PLAN_NAMES[p.kind] ?? p.kind),
      route: jump && {
        distance: wholeLightYears(jump.totalDistance),
        jumps: number(jump.totalJumps),
        reached: stops.length > 0 && reached >= 0 ? `${Math.min(reached + 1, stops.length)} OF ${stops.length} WAYPOINTS` : null,
        reachedPct: stops.length > 0 && reached >= 0 ? clamp(((reached + 1) / stops.length) * 100, 0, 100) : 0,
        total: stops.length,
        rows: shown.map((w, i) => ({
          n: start + i + 1,
          system: String(w?.system ?? ''),
          jumps: number(w?.jumps),
          left: finite(w?.distanceLeft) === null ? '—' : `${number(w.distanceLeft)} LY`,
          neutron: Boolean(w?.isNeutron),
          reached: start + i <= reached,
          phone: `${plural(w?.jumps ?? 0, 'jump')}${w?.isNeutron ? ' · neutron' : ''}${start + i <= reached ? ' · reached' : ''}`,
        })),
        more: Math.max(0, stops.length - (start + shown.length)),
        destination: jump.destination,
      },
    };
  }

  return { rows, detail };
}

// ---------------------------------------------------------------------------------------------
// Header and summaries

const sectionOf = {
  progress: (doc) => doc.progress,
  checklist: (doc) => doc.checklist,
  watches: (doc) => doc.watches,
  missions: (doc) => doc.missions,
  ships: (doc) => doc.ships,
  carrier: (doc) => doc.carrier,
  engineers: (doc) => doc.engineers,
  materials: (doc) => doc.materials,
  plan: (doc) => doc.routePlans,
};

/** Whether the snapshot carries the section this page draws. The account page needs none. */
export const hasSection = (doc, page) => !sectionOf[page] || (sectionOf[page](doc) ?? null) !== null;

const SUMMARIES = {
  progress: () => 'Credits, ranks, reputation and where you were at the snapshot.',
  checklist: (doc) => {
    const v = checklistView(doc, null);
    return v ? `${plural(v.goals.length, 'goal')}. ${plural(v.listCount, 'list')}, ${plural(v.openLines, 'line')} open.` : '';
  },
  watches: (doc) => `What D47 is watching for you. ${array(doc.watches).length} OF ${MOST_WATCHES}.`,
  missions: (doc, now) => {
    const v = missionsView(doc, now);
    return v ? `${plural(v.count, 'mission')}. ${number(v.total)} credits in rewards.` : '';
  },
  ships: (doc) => shipsView(doc)?.summary ?? '',
  carrier: () => 'Your fleet carrier, as the game last reported it.',
  engineers: (doc) => engineersView(doc, null)?.summary ?? '',
  materials: () => "Ship materials by grade, against each grade's cap.",
  plan: () => 'The routes you plotted in D47. Plot new ones there.',
  account: () => 'Who is signed in, and the snapshot D47 stores for them.',
};

export function summaryOf(doc, page, now) {
  return hasSection(doc, page) ? SUMMARIES[page]?.(doc, now) ?? '' : '';
}

const ABSENT = {
  progress: 'This snapshot has no progress figures.',
  checklist: 'This snapshot has no checklist or goals.',
  watches: 'This snapshot has no watches.',
  missions: 'This snapshot has no missions.',
  ships: 'This snapshot has no ships.',
  carrier: 'This snapshot has no fleet carrier.',
  engineers: 'This snapshot has no engineer standings.',
  materials: 'This snapshot has no ship materials.',
  plan: 'This snapshot has no route plans.',
};

/** What a page says when the snapshot has no section for it. */
export const absentLine = (page) => ABSENT[page] ?? 'This snapshot has nothing for this page.';

// ---------------------------------------------------------------------------------------------
// Empty states

export function emptyStateView(state) {
  switch (state.kind) {
    case 'linked':
      return {
        context: 'Signed in',
        title: 'No snapshot yet',
        lead: 'D47 on your PC has uploads turned on for this Commander, and has not sent a snapshot yet. The first one arrives after D47 next reads your journal.',
        what: 'Your ranks, ships, materials, engineers, missions, carrier, checklist, watches and route plans, from D47 on your PC.',
        hint: 'This page asks for the snapshot again every minute while it is open.',
      };
    case 'off': {
      const since = state.since;
      return {
        context: 'Signed in with Frontier',
        title: 'Uploads turned off',
        lead: 'D47 on your PC turned uploads off, and the snapshot it had sent was deleted. Nothing is stored for this Commander.',
        detail: since ? `UPLOADS OFF SINCE ${dayMonth(since)} · ${clock(since)}` : null,
        hint: 'Turn uploads on in D47 to see a snapshot here again.',
      };
    }
    case 'error':
      return {
        context: 'Signed in with Frontier',
        title: 'Could not load the snapshot',
        lead: 'The server did not answer the request for the snapshot. This page tries again every minute.',
      };
    default:
      return {
        context: 'Signed in with Frontier',
        title: 'Nothing uploaded for this account',
        lead: 'D47 has never uploaded a snapshot for the Frontier account you signed in with. Turn uploads on in D47 on your PC, for the Commander on this account.',
        notice: {
          label: 'More than one Frontier account',
          text: 'If D47 uploads for a Commander on another Frontier account, sign out and sign in with that one.',
        },
      };
  }
}

export const SIGNED_OUT = {
  context: 'Directive 47 on the web',
  title: 'Sign in, Commander',
  lead: 'This page shows your Commander as D47 last uploaded it: ranks, ships, materials, engineers, missions, your carrier and your plans. It reads only. Nothing here changes the game or D47.',
  shares:
    'Frontier tells D47 your customer number and nothing else is kept: not your name, your email address or your Frontier tokens. The number matches this page to the snapshot your PC uploaded.',
  hint: 'Uploads come from D47 on your PC. Turn them on there first.',
};
