// The functions that turn a section of the snapshot document into what a page draws, run against a
// fixture document, and the rules the static pages keep. Run with `node --test` from this folder.

import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';

import { CONTENT_SECURITY_POLICY } from './src/index.js';
import * as v from './public/js/views.js';

const fixture = () => JSON.parse(readFileSync(new URL('./fixtures/snapshot.json', import.meta.url), 'utf8'));
const NOW = Date.parse('2026-11-12T21:42:00Z');

test('a 200 draws the snapshot and each 404 draws its own state', () => {
  assert.equal(v.stateOf(200, fixture()).kind, 'snapshot');
  assert.equal(v.stateOf(401, null).kind, 'signedOut');
  assert.equal(v.stateOf(404, { state: 'linked' }).kind, 'linked');
  assert.equal(v.stateOf(404, { state: 'none' }).kind, 'none');
  assert.equal(v.stateOf(404, null).kind, 'none');
  // The Worker writes the switch-off time as new Date(ms).toISOString().
  const off = v.stateOf(404, { state: 'off', since: new Date(Date.parse('2026-11-12T19:55:00Z')).toISOString() });
  assert.equal(off.kind, 'off');
  assert.equal(off.since.toISOString(), '2026-11-12T19:55:00.000Z');
  assert.equal(v.stateOf(500, null).kind, 'error');
});

test('the empty states say what the design says, and name no Commander', () => {
  assert.equal(v.emptyStateView({ kind: 'linked' }).title, 'No snapshot yet');
  assert.equal(v.emptyStateView({ kind: 'none' }).title, 'Nothing uploaded for this account');
  const off = v.emptyStateView({ kind: 'off', since: new Date('2026-11-12T19:55:00Z') });
  assert.equal(off.title, 'Uploads turned off');
  assert.equal(off.detail, 'UPLOADS OFF SINCE 12 NOV · 19:55 UTC');
  assert.equal(v.whoOf({ kind: 'linked' }), 'FRONTIER ACCOUNT');
  assert.equal(v.whoOf({ kind: 'none' }), 'FRONTIER ACCOUNT');
  assert.equal(v.whoOf({ kind: 'snapshot', doc: fixture() }), 'CMDR ILSA MARROW');
});

test('a snapshot older than 30 days draws its age in amber with OLD SNAPSHOT', () => {
  const fresh = v.ageView('2026-11-12T19:42:00Z', NOW);
  assert.equal(fresh.old, false);
  assert.equal(fresh.top, 'SNAPSHOT 19:42 UTC');
  assert.equal(fresh.bottom, '2 hours ago');

  const old = v.ageView('2026-10-03T19:42:00Z', NOW);
  assert.equal(old.old, true);
  assert.equal(old.top, 'SNAPSHOT 3 OCT · 40 DAYS AGO');
  assert.equal(old.bottom, 'OLD SNAPSHOT');
  assert.match(old.line, /OLD SNAPSHOT$/);

  assert.equal(v.ageView('2026-10-13T21:42:00Z', NOW).old, false);
  assert.equal(v.ageView('2026-10-13T21:41:00Z', NOW).old, true);
  assert.equal(v.ageView('not a time', NOW), null);
});

test('the theme is the snapshot theme, and Elite where there is none or it is unknown', () => {
  assert.equal(v.themeOf({ kind: 'signedOut' }).id, 'elite');
  assert.equal(v.themeOf({ kind: 'linked' }).id, 'elite');
  assert.equal(v.themeOf({ kind: 'snapshot', doc: { ...fixture(), theme: 'light' } }).id, 'light');
  assert.equal(v.themeOf({ kind: 'snapshot', doc: { ...fixture(), theme: 'neon' } }).id, 'elite');
  assert.deepEqual(v.themeOf({ kind: 'snapshot', doc: { ...fixture(), theme: 'dark', hudMatrix: [1, 0, 0, 0, 1, 0, 0, 0, 1] } }).vars, {});
});

test('My HUD colours recolours the coloured tokens through the matrix and leaves the neutrals', () => {
  const swap = [0, 0, 1, 0, 1, 0, 1, 0, 0];
  const theme = v.themeOf({ kind: 'snapshot', doc: { ...fixture(), theme: 'elite-palette', hudMatrix: swap } });
  assert.equal(theme.id, 'elite-palette');
  assert.equal(theme.vars['--d47-a'], '#1a7aff');
  assert.equal(theme.vars['--d47-cyan'], '#e8d633');
  assert.deepEqual(Object.keys(theme.vars).sort(), ['--d47-a', '--d47-blue', '--d47-brown', '--d47-cyan', '--d47-knock', '--d47-red', '--d47-warn', '--d47-yellow']);

  const identity = v.themeOf({ kind: 'snapshot', doc: { ...fixture(), theme: 'elite-palette', hudMatrix: [1, 0, 0, 0, 1, 0, 0, 0, 1] } });
  assert.equal(identity.vars['--d47-a'], '#ff7a1a');
  assert.equal(v.themeOf({ kind: 'snapshot', doc: { ...fixture(), theme: 'elite-palette' } }).id, 'elite-palette');
  assert.deepEqual(v.themeOf({ kind: 'snapshot', doc: { ...fixture(), theme: 'elite-palette', hudMatrix: [1, 2] } }).vars, {});
});

test('ranks are named from the ladders, and a navy rank from its own', () => {
  const ranks = v.progressView(fixture());
  assert.deepEqual(ranks.careers.map((c) => c.career), ['Combat', 'Trade', 'Explore', 'Mercenary', 'Exobiologist', 'CQC']);
  assert.deepEqual(ranks.careers[0], { career: 'Combat', rank: 'DANGEROUS', meta: 'RANK 6 OF 13 · 34%', pct: 34 });
  assert.equal(ranks.careers[1].rank, 'ELITE II');
  assert.equal(ranks.careers[2].rank, 'ELITE');
  assert.equal(ranks.careers[3].rank, 'SOLDIER');
  assert.deepEqual(ranks.navies.map((n) => [n.career, n.rank, n.meta]), [
    ['Imperial navy', 'VISCOUNT', 'RANK 8 OF 14 · 41%'],
    ['Federal navy', 'PETTY OFFICER', 'RANK 4 OF 14 · 88%'],
  ]);
});

test('reputation is read in the bands the game uses, on a 0 to 100 gauge', () => {
  const [empire, federation, alliance, independents] = v.progressView(fixture()).reputation;
  assert.deepEqual(empire, { power: 'Empire', word: 'ALLIED', num: '+92', pct: 96 });
  assert.equal(federation.word, 'FRIENDLY');
  assert.equal(alliance.word, 'NEUTRAL');
  assert.equal(alliance.num, '−6');
  assert.equal(independents.power, 'Independents');
  assert.equal(v.reputationBand(-100), 'HOSTILE');
  assert.equal(v.reputationBand(3.9), 'NEUTRAL');
  assert.equal(v.reputationBand(4), 'CORDIAL');
});

test('Progress draws the location the snapshot has and no tile for what it has not', () => {
  const view = v.progressView(fixture());
  assert.equal(view.balance, '1,284,550,120 CR');
  assert.deepEqual(view.tiles.map((t) => t.label), ['Current system', 'Docked at', 'Flight', 'Body', 'Controlling faction', 'Powerplay']);

  const doc = fixture();
  delete doc.progress.location.stationName;
  assert.ok(!v.progressView(doc).tiles.some((t) => t.label === 'Docked at'));
});

test('time left counts from the viewer clock and the bands follow from it', () => {
  assert.deepEqual(v.timeLeft('2026-11-12T22:20:00Z', NOW), { text: '0:38', expired: false, ms: 38 * 60000 });
  assert.equal(v.timeLeft('2026-11-13T16:42:00Z', NOW).text, '19H');
  assert.equal(v.timeLeft('2026-11-14T23:42:00Z', NOW).text, '2D 2H');
  assert.equal(v.timeLeft('2026-11-12T21:00:00Z', NOW).text, 'Expired');
  assert.equal(v.timeLeft(undefined, NOW).text, '—');

  const view = v.missionsView(fixture(), NOW);
  assert.deepEqual(view.bands.map((b) => [b.title, b.count]), [['Expiring within the hour', 1], ['Hand in here', 2], ['Everything else', 2]]);
  assert.equal(view.bands[0].rows[0].title, 'Deliver 24 T of Gold');
  assert.equal(view.bands[0].rows[0].left, '0:38');
  assert.equal(view.bands[0].rows[0].urgent, true);
  assert.deepEqual(view.bands[1].rows.map((r) => r.title), ['Transport 8 passengers', 'Assassinate a pirate lord']);
  assert.equal(view.bands[2].rows[1].left, '—');
  assert.equal(view.total, 27540000);
  assert.equal(view.bands[2].rows[0].extra, 'Cargo 12 of 40 delivered');

  const later = v.missionsView(fixture(), Date.parse('2026-11-12T22:00:00Z'));
  assert.equal(later.bands[0].rows[0].left, '0:20');
});

test('a mission is Hand in here only for the system, and the station when both are known', () => {
  const here = { starSystem: 'Giryak', stationName: 'Ziegel Hub' };
  assert.equal(v.missionBand({ destinationSystem: 'Giryak', destinationStation: 'Ziegel Hub', expiry: '2027-01-01T00:00:00Z' }, here, NOW), 'here');
  assert.equal(v.missionBand({ destinationSystem: 'Giryak', destinationStation: 'Other Hub', expiry: '2027-01-01T00:00:00Z' }, here, NOW), 'rest');
  assert.equal(v.missionBand({ destinationSystem: 'Giryak', expiry: '2027-01-01T00:00:00Z' }, here, NOW), 'here');
  assert.equal(v.missionBand({ destinationSystem: 'Giryak', expiry: '2026-11-12T22:00:00Z' }, here, NOW), 'hour');
});

test('Ships draws the flown ship in full and a stored ship by its list line', () => {
  const view = v.shipsView(fixture());
  assert.equal(view.flownLabel, 'Cold Iron (Krait Mk II)');
  assert.equal(view.summary, '5 ships. Flying Cold Iron in Shinrarta Dezhra.');
  assert.deepEqual(view.stored.map((s) => s.where), [
    'Parked at Jameson Memorial, Shinrarta Dezhra.',
    'In transit to Shinrarta Dezhra.',
    'Parked at Ray Gateway, Diaso.',
    'Parked in Lave.',
  ]);
  assert.equal(view.stored[2].label, 'Mamba');
  assert.equal(view.stored[3].value, '');
  assert.deepEqual(view.detail.tiles.map((t) => t.value), ['32.41 LY', '64 T', '32 T', '412.6 T', '68,240,190 CR', '100%']);
  assert.equal(view.detail.rebuy, '3,412,050 CR');
  assert.deepEqual(view.detail.modules[0], {
    item: 'Multi-cannon 3B gimballed', slot: 'Large hardpoint 1', engineering: 'Overcharged · G5', engineered: true,
    experimental: 'Corrosive shell', health: '100%',
  });
  assert.equal(view.detail.modules[3].engineering, 'Not engineered');
  assert.equal(view.detail.modules[4].health, '—');
});

test('Carrier works out what the snapshot leaves to the page', () => {
  const view = v.carrierView(fixture(), NOW);
  const byLabel = Object.fromEntries(view.tiles.map((t) => [t.label, t]));
  assert.equal(view.title, 'Sacred Fire');
  assert.equal(view.context, 'Fleet carrier · ZZT-99A');
  assert.equal(byLabel.Space.value, '1,110 / 25,000 T');
  assert.equal(Math.round(byLabel.Space.gauge * 10) / 10, 4.4);
  assert.equal(byLabel.Covers.value, '153 WEEKS');
  assert.equal(byLabel['Jumping to'].note, 'DEPARTS 22:05 UTC');
  assert.equal(byLabel.Docking.value, 'ALL');
  assert.deepEqual(view.tritium.map((t) => t.value), ['967 / 1,000 T', '120 T', '1,087 T']);
  assert.equal(view.open, 'REFUEL · REPAIR');
  assert.equal(view.off, 'BARTENDER');
});

test('a carrier departure that has passed is not drawn as pending', () => {
  const view = v.carrierView(fixture(), Date.parse('2026-11-12T23:00:00Z'));
  assert.equal(view.tiles.find((t) => t.label === 'Jumping to').note, null);
});

test('Engineers are grouped by how near they are to unlocking, with the distance from the snapshot location', () => {
  const view = v.engineersView(fixture(), null);
  assert.deepEqual(view.groups.map((g) => g.title), ['Ready for unlock', 'Unlocked', 'Needs a referral']);
  assert.equal(view.summary, '2 of 5 unlocked. 2 ready for unlock. 1 need a referral.');
  assert.deepEqual(view.groups[0].rows.map((r) => [r.name, r.sub, r.aside]), [
    ['Tiana Fortune', 'Invited', '177 LY · ~4 jumps'],
    ['Broo Tarquin', 'Known · no invitation', '230 LY · ~6 jumps'],
  ]);
  assert.equal(view.groups[1].rows[1].sub, 'Grade 4 · 62%');
  assert.equal(view.groups[2].rows[0].sub, 'Referred by Professor Palin');
  assert.equal(view.detail.title, 'Tiana Fortune');

  const broo = v.engineersView(fixture(), '0').detail;
  assert.equal(broo.title, 'Broo Tarquin');
  assert.deepEqual(broo.tiles.map((t) => t.label), ['Workshop', 'Distance from Shinrarta Dezhra', 'Where you stand', 'Grades', 'Unlock cost']);
  assert.deepEqual(broo.prerequisites.map((p) => [p.state, p.gauge]), [['✓ Met', null], ['In progress', 34], ['? Unknown', null]]);
});

test('Materials are grouped by grade against the cap for that grade', () => {
  const raw = v.materialsView(fixture(), null);
  assert.equal(raw.chosen, 'Raw');
  assert.equal(raw.kinds[0].status, '5 kinds · 622 units');
  assert.deepEqual(raw.grades.map((g) => [g.title, g.cap]), [['Grade 1', 'CAP 300 EACH'], ['Grade 2', 'CAP 250 EACH'], ['Grade 3', 'CAP 200 EACH'], ['Grade 4', 'CAP 150 EACH']]);
  assert.deepEqual(raw.grades[0].items.map((m) => [m.name, m.count, m.pct]), [['Carbon', '300 / 300', 100], ['Iron', '212 / 300', 70.7]]);

  const encoded = v.materialsView(fixture(), 'Encoded');
  assert.deepEqual(encoded.grades.map((g) => g.title), ['Grade 2', 'Ungraded']);
  assert.deepEqual(encoded.grades[1].items[0], { name: 'Unlisted Oddity', count: '3', pct: null });
});

test('Checklist draws the lists by home, the chosen list as a ladder and goals as gauges', () => {
  const view = v.checklistView(fixture(), null);
  assert.deepEqual(view.groups.map((g) => g.title), ['Yours', 'Ships', 'Systems', 'Suits and weapons']);
  assert.equal(view.detail.title, 'Cold Iron');
  assert.equal(view.detail.context, 'Ships · flying now');
  assert.deepEqual(view.detail.lines.map((l) => l.state), ['✓ Done', 'Open', 'Elsewhere', 'Blocked', '? Unverified']);
  assert.equal(view.groups[1].rows[0].sub, 'Flying now');
  assert.equal(view.groups[3].rows[0].sub, 'Every line done');
  assert.equal(view.groups[3].rows[0].aside, '4 done');
  assert.deepEqual(view.goals[1], { goal: 'A billion credits', line: '✓ MET', pct: 100, figures: '1,284,550,120 OF 1,000,000,000' });
  assert.equal(v.checklistView(fixture(), '0').detail.title, 'Before the Colonia trip');
  assert.equal(v.summaryOf(fixture(), 'checklist', NOW), '2 goals. 5 lists, 14 lines open.');
});

test('Watches are told apart by kind and show what fired', () => {
  const stock = v.watchesView(fixture(), null);
  assert.deepEqual(stock.kinds.map((k) => k.status), ['3 watches', '1 watch', '1 watch']);
  assert.deepEqual(stock.rows.map((r) => [r.aside, r.tone]), [['Fired 18:20, 2,410 in stock', 'fired'], ['Watching', ''], ['Off', 'off']]);
  assert.equal(stock.rows[0].sub, 'Above 2,000 · Ziegel Hub, Giryak');
  assert.equal(v.watchesView(fixture(), 'NextSystem').rows[0].sub, 'Material trader · Raw · Large pad or larger');
  assert.equal(v.summaryOf(fixture(), 'watches', NOW), 'What D47 is watching for you. 5 OF 32.');
});

test('Plan draws the jump route as far as it goes and lists the other plans by headline', () => {
  const view = v.planView(fixture(), null);
  assert.deepEqual(view.rows.map((r) => [r.name, r.aside]), [['Neutron Plotter', '11 Nov'], ['Road to Riches', '8 Nov'], ['Carrier Route', '12 Nov']]);
  const route = view.detail.route;
  assert.equal(route.reached, '3 OF 5 WAYPOINTS');
  assert.equal(route.distance, '22,011 LY');
  assert.deepEqual(route.rows.map((r) => [r.n, r.reached]), [[1, true], [2, true], [3, true], [4, false], [5, false]]);
  assert.equal(route.more, 0);
  assert.equal(v.planView(fixture(), '1').detail.route, null);
});

test('a section the snapshot does not have is absent, and a higher format still draws what is known', () => {
  const doc = fixture();
  doc.format = 9;
  doc.somethingNew = { nested: true };
  doc.progress.somethingNew = 1;
  delete doc.ships;
  delete doc.routePlans;
  assert.equal(v.hasSection(doc, 'ships'), false);
  assert.equal(v.hasSection(doc, 'plan'), false);
  assert.equal(v.hasSection(doc, 'progress'), true);
  assert.equal(v.hasSection(doc, 'account'), true);
  assert.equal(v.progressView(doc).tiles.length, 6);
  assert.equal(v.summaryOf(doc, 'ships', NOW), '');
  assert.equal(v.absentLine('ships'), 'This snapshot has no ships.');
  assert.equal(v.absentLine('carrier'), 'This snapshot has no fleet carrier.');
});

test('a document with nothing but its identity draws without throwing', () => {
  const bare = { format: 1, frontierId: 'F1', name: 'Test Pilot', takenAt: '2026-11-12T19:42:00Z', theme: 'dark' };
  for (const page of Object.keys(v.PAGES)) {
    assert.doesNotThrow(() => v.summaryOf(bare, page, NOW), page);
  }
  assert.equal(v.hasSection(bare, 'missions'), false);
});

test('routes name a page and an entry, and anything else is Progress', () => {
  assert.deepEqual(v.parseRoute('#/ships/2'), { page: 'ships', argument: '2' });
  assert.deepEqual(v.parseRoute('#/watches/NextSystem'), { page: 'watches', argument: 'NextSystem' });
  assert.deepEqual(v.parseRoute(''), { page: 'progress', argument: null });
  assert.deepEqual(v.parseRoute('#/__proto__'), { page: 'progress', argument: null });
  assert.equal(v.routeHref('ships', 2), '#/ships/2');
  assert.equal(v.tabOf('materials').id, 'assets');
});

test('the snapshot is asked for again no more than once a minute and not while hidden', () => {
  assert.equal(v.shouldRefresh({ now: 1000, lastAsked: null, hidden: false }), true);
  assert.equal(v.shouldRefresh({ now: 59_000, lastAsked: 0, hidden: false }), false);
  assert.equal(v.shouldRefresh({ now: 60_000, lastAsked: 0, hidden: false }), true);
  assert.equal(v.shouldRefresh({ now: 600_000, lastAsked: 0, hidden: true }), false);
  assert.equal(v.shouldRefresh({ now: 600_000, lastAsked: 0, hidden: false, signedOut: true }), false);
});

// ---------------------------------------------------------------------------------------------
// The static pages

const PUBLIC = new URL('./public/', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');

function* files(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* files(path);
    else yield path;
  }
}

test('no page writes markup, an inline style or an inline script', () => {
  const ours = [...files(PUBLIC)].filter((p) => /\.(js|mjs|html)$/.test(p) || /[\\/]web\.css$/.test(p));
  assert.ok(ours.length >= 5);
  const forbidden = [/innerHTML/, /outerHTML/, /insertAdjacentHTML/, /document\.write/, /\.setAttribute\(\s*['"]style/, /\beval\(/, /new Function/, /\.setAttribute\(\s*['"]on/];
  for (const path of ours) {
    const source = readFileSync(path, 'utf8');
    for (const pattern of forbidden) assert.doesNotMatch(source, pattern, `${path} matches ${pattern}`);
    if (path.endsWith('.html')) {
      assert.doesNotMatch(source, /<script(?![^>]*\ssrc=)/i, `${path} has an inline script`);
      assert.doesNotMatch(source, /<[^>]*\s(?:style|on[a-z]+)\s*=/i, `${path} has an inline style or handler`);
    }
  }
});

test('the pages request nothing from any other host', () => {
  for (const path of files(PUBLIC)) {
    if (/\.(ttf|txt)$/.test(path)) continue;
    const source = readFileSync(path, 'utf8');
    const hosts = [...source.matchAll(/(?:https?:)?\/\/[a-z0-9.-]+\.[a-z]{2,}/gi)].map((m) => m[0]);
    assert.deepEqual(hosts, [], `${path} names a host`);
  }
});

test('the Content Security Policy allows the Worker origin only and matches the static headers', () => {
  const headers = readFileSync(new URL('./public/_headers', import.meta.url), 'utf8');
  assert.ok(headers.includes(`Content-Security-Policy: ${CONTENT_SECURITY_POLICY}`));
  assert.match(CONTENT_SECURITY_POLICY, /^default-src 'none'/);
  for (const directive of CONTENT_SECURITY_POLICY.split('; ')) {
    const sources = directive.split(' ').slice(1);
    assert.ok(sources.every((s) => s === "'self'" || s === "'none'"), directive);
  }
});

test('the pages are linked as modules from the Worker, with the sign-in and sign-out routes it has', () => {
  const html = readFileSync(new URL('./public/index.html', import.meta.url), 'utf8');
  assert.match(html, /<script type="module" src="js\/app.js">/);
  const dom = readFileSync(new URL('./public/js/dom.js', import.meta.url), 'utf8');
  assert.match(dom, /href: '\/signin'/);
  const app = readFileSync(new URL('./public/js/app.js', import.meta.url), 'utf8');
  assert.match(app, /url: '\/signout', method: 'POST'/);
  assert.match(app, /url: '\/account', method: 'DELETE'/);
  assert.match(app, /fetch\('\/snapshot'/);
});
