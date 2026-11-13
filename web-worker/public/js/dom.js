// Builds the page from the view objects in views.js. Every value is written with createTextNode or
// textContent, never as markup, and styles are set through the CSSOM so the Content Security
// Policy can refuse inline styles.

import {
  PAGES,
  SIGNED_OUT,
  TABS,
  absentLine,
  ageView,
  carrierView,
  checklistView,
  emptyStateView,
  engineersView,
  hasSection,
  materialsView,
  missionsView,
  planView,
  progressView,
  routeHref,
  shipsView,
  summaryOf,
  tabOf,
  watchesView,
  whoOf,
} from './views.js';

export function h(tag, props = {}, ...kids) {
  const el = document.createElement(tag);
  for (const [name, value] of Object.entries(props ?? {})) {
    if (value === null || value === undefined || value === false) continue;
    if (name === 'class') el.className = value;
    else if (name === 'style') for (const [property, v] of Object.entries(value)) el.style.setProperty(property, v);
    else if (name === 'on') for (const [type, listener] of Object.entries(value)) el.addEventListener(type, listener);
    else el.setAttribute(name, value === true ? '' : String(value));
  }
  append(el, kids);
  return el;
}

function append(el, kids) {
  for (const kid of kids.flat(Infinity)) {
    if (kid === null || kid === undefined || kid === false) continue;
    el.append(kid instanceof Node ? kid : document.createTextNode(String(kid)));
  }
}

const div = (cls, ...kids) => h('div', { class: cls }, ...kids);
const span = (cls, ...kids) => h('span', { class: cls }, ...kids);
const gauge = (pct, cls = '') => div(`d47-gauge ${cls}`.trim(), h('div', { class: 'd47-gauge__fill', style: { width: `${pct}%` } }));
const sectionHead = (title, aside) => div('d47-section-head', title, aside ? span('d47-section-head__aside', aside) : null);
const hint = (words) => span('d47-hint web__hint', words);

function titleBlock(context, title, figureLabel, figureValue) {
  return div(
    'd47-title-block',
    div('d47-title-block__text', span('d47-title-block__context', context), span('d47-title', title)),
    figureValue ? div('d47-title-block__figure', span('d47-title-block__figure-label', figureLabel), span('d47-title-block__figure-value', figureValue)) : null,
  );
}

function tile({ label, value, tone = '', mono = false, note = null, wide = false }) {
  const cls = `d47-data__value ${tone.replace(/mono/, 'd47-mono')} ${mono ? 'd47-mono' : ''}`.trim();
  return div(`d47-data ${wide ? 'span-2' : ''}`.trim(), span('d47-data__label', label), span(cls, value), note ? span('web__meta', note) : null);
}

function gaugeTile({ label, value, pct, valueTone = 'yellow small', capacity = true }) {
  return div(
    'd47-data web__tight',
    div('d47-data__row', span('d47-data__label', label), span(`d47-data__value ${valueTone} d47-data__end`, value)),
    gauge(pct, capacity ? 'capacity' : ''),
  );
}

function meterCard(card) {
  return div(
    'd47-data web__tight',
    div('d47-data__row', span('d47-data__label', card.career ?? card.power), span('d47-data__end d47-mono web__meta', card.meta ?? card.num)),
    span('d47-data__value', card.rank ?? card.word),
    gauge(card.pct),
  );
}

// ---------------------------------------------------------------------------------------------
// Shell

function bar(state, now, navigate, ui) {
  const age = state.kind === 'snapshot' ? ageView(state.doc.takenAt, now) : null;
  const who = whoOf(state);
  const signedIn = state.kind !== 'signedOut';

  return h(
    'header',
    { class: 'web__bar' },
    span('d47-brand-mark', ''),
    span('d47-brand-name', 'DIRECTIVE 47'),
    who ? span('web__sep') : null,
    who ? span('web__cmdr', who) : null,
    div(
      'web__end',
      age ? span(`web__age ${age.old ? 'old' : ''}`.trim(), span('', age.top), span(age.old ? 'web__age-flag' : '', age.bottom)) : null,
      state.kind === 'snapshot' ? h('a', { class: `d47-tile-button web__account ${ui.page === 'account' ? 'selected' : ''}`.trim(), href: routeHref('account') }, 'Account') : null,
      state.kind === 'snapshot'
        ? h('button', { class: 'd47-tile-button web__menu', type: 'button', 'aria-expanded': ui.menuOpen, on: { click: () => navigate({ menu: !ui.menuOpen }) } }, ui.menuOpen ? 'Menu ▲' : 'Menu ▼')
        : null,
      signedIn && state.kind !== 'snapshot' ? signOutButton(ui) : null,
    ),
  );
}

const signOutButton = (ui) => h('button', { class: 'd47-tile-button tall', type: 'button', on: { click: ui.signOut } }, 'Sign out');

function menuPanel(ui) {
  const row = (page, sub) =>
    h(
      'a',
      { class: `d47-list-row ${ui.page === page ? 'selected' : ''}`.trim(), href: routeHref(page), on: { click: () => ui.closeMenu() } },
      div('d47-list-row__main', span('d47-list-row__name', PAGES[page].label), sub ? span('d47-list-row__sub', sub) : null),
    );
  return h(
    'nav',
    { class: 'web__menupanel d47-list', 'aria-label': 'Pages' },
    div('d47-list-head', 'Commander'),
    ...['checklist', 'watches', 'missions', 'progress'].map((p) => row(p)),
    div('d47-list-head', 'Asset mgmt'),
    ...['ships', 'carrier', 'engineers', 'materials'].map((p) => row(p)),
    div('d47-list-head', 'Navigation'),
    row('plan'),
    div('d47-list-head', 'You'),
    row('account', 'Sign out, or delete the snapshot'),
  );
}

function header(doc, page, now) {
  const tab = tabOf(page);
  const tabs = TABS.map((t) =>
    h('a', { class: `d47-tab ${tab?.id === t.id ? 'active' : ''}`.trim(), href: routeHref(t.home) }, t.label),
  );
  const title = PAGES[page].title;
  const sub = tab
    ? div(
        'd47-subbar',
        h(
          'nav',
          { class: 'd47-subtabs', 'aria-label': tab.groupLabel },
          ...tab.pages.map((p) => h('a', { class: `d47-subtab ${p === page ? 'active' : ''}`.trim(), href: routeHref(p) }, PAGES[p].label)),
        ),
      )
    : null;

  return div(
    'd47-header',
    div(
      'd47-header__top',
      div(
        'd47-header__main',
        h('nav', { class: 'd47-tabs', 'aria-label': 'Tabs' }, tabs),
        div('d47-tabs-rule'),
        div('d47-header__title-line', h('h1', { class: 'd47-header__title' }, title), span('d47-header__summary', summaryOf(doc, page, now))),
      ),
    ),
    div('d47-header__rule'),
    sub,
  );
}

// ---------------------------------------------------------------------------------------------
// Pages

function absent(page) {
  return div('web__section', h('p', { class: 'web__p web__grey' }, absentLine(page)));
}

function progressPage(doc) {
  const v = progressView(doc);
  return [
    div(
      'web__section',
      titleBlock('Commander record', v.name, 'Credits', v.balance),
      v.tiles.length ? div('d47-data-grid', v.tiles.map(tile)) : null,
    ),
    v.careers.length ? div('web__section', sectionHead('Ranks'), div('d47-data-grid', v.careers.map(meterCard))) : null,
    v.navies.length ? div('web__section', sectionHead('Navy ranks'), div('d47-data-grid', v.navies.map(meterCard))) : null,
    v.reputation.length
      ? div('web__section', sectionHead('Reputation', '−100 to 100'), div('d47-data-grid four', v.reputation.map(meterCard)))
      : null,
  ];
}

function listRows(groups, page) {
  return groups.map((group) => [
    div('d47-list-head', group.title),
    group.rows.map((row) =>
      h(
        'a',
        { class: `d47-list-row ${row.selected ? 'selected' : ''}`.trim(), href: routeHref(page, row.index) },
        div('d47-list-row__main', span('d47-list-row__name', row.name), row.sub ? span('d47-list-row__sub', row.sub) : null),
        row.aside ? span('d47-list-row__aside', row.aside) : null,
      ),
    ),
  ]);
}

function ladder(lines) {
  return div(
    'd47-ladder',
    lines.map((line) =>
      div(
        'd47-ladder__row',
        span(`d47-ladder__state ${line.cls}`.trim(), line.state),
        span('d47-ladder__text', line.text),
        line.gauge !== null && line.gauge !== undefined ? [span(), gauge(line.gauge)] : null,
      ),
    ),
  );
}

function checklistPage(doc, argument) {
  const v = checklistView(doc, argument);
  return [
    v.goals.length
      ? div(
          'web__section',
          sectionHead('Goals'),
          div(
            'd47-data-grid',
            v.goals.map((g) =>
              div(
                'd47-data web__tight',
                div('d47-data__row', span('d47-data__label web__white', g.goal), span('d47-data__end d47-mono web__meta', `${g.pct}%`)),
                g.line ? span('d47-data__value small', g.line) : null,
                gauge(g.pct),
                g.figures ? span('d47-mono web__meta', g.figures) : null,
              ),
            ),
          ),
        )
      : null,
    v.groups.length
      ? div(
          'split',
          h('nav', { class: 'd47-list', 'aria-label': 'Lists' }, listRows(v.groups, 'checklist')),
          v.detail
            ? div('web__section', titleBlock(v.detail.context, v.detail.title), ladder(v.detail.lines))
            : null,
        )
      : null,
  ];
}

function watchesPage(doc, argument) {
  const v = watchesView(doc, argument);
  return [
    h(
      'nav',
      { class: 'd47-segmented cols-3', 'aria-label': 'Watch kinds' },
      v.kinds.map((k) =>
        h(
          'a',
          { class: `d47-segment has-status ${k.kind === v.chosen ? 'selected' : ''}`.trim(), href: routeHref('watches', k.kind) },
          span('d47-segment__label', k.label),
          span('d47-segment__status', k.status),
        ),
      ),
    ),
    div(
      'web__section web__narrow',
      sectionHead(v.title),
      v.rows.length
        ? div(
            'd47-list',
            v.rows.map((r) =>
              div(
                'd47-list-row web__slab',
                div('d47-list-row__main', span('d47-list-row__name', r.name), r.sub ? span('d47-list-row__sub', r.sub) : null),
                span(`d47-list-row__aside ${r.tone === 'fired' ? 'web__cyan' : r.tone === 'off' ? 'web__grey' : ''}`.trim(), r.aside),
              ),
            ),
          )
        : span('d47-hint web__hint', 'No watches of this kind.'),
      hint('D47 on your PC does the watching. Add or change watches there, or by voice.'),
    ),
  ];
}

function missionsPage(doc, now) {
  const v = missionsView(doc, now);
  return [
    hint("Time left counts down from each mission's expiry on your clock, not from the snapshot."),
    v.bands.map((band) =>
      div(
        'web__section',
        sectionHead(band.title, String(band.count)),
        div(
          'wt web__missions',
          div(
            'd47-sortrow',
            div('d47-sorthead', span('d47-sorthead__label', 'Mission'), span('d47-sorthead__second', 'Given by')),
            div('d47-sorthead', span('d47-sorthead__label', 'Hand in at')),
            div('d47-sorthead right', span('d47-sorthead__label', 'Time left')),
            div('d47-sorthead right', span('d47-sorthead__label', 'Reward')),
          ),
          band.rows.map((m) =>
            div(
              'wrow',
              div('two', span('name', m.title), span('', m.giver)),
              div(
                'two ph-line',
                span('', m.station, m.system ? [' ', span('sys', `(${m.system})`)] : null),
                span('', m.extra),
              ),
              span(`num ph-hide ${m.urgent ? 'warn' : ''}`.trim(), m.left),
              div('two web__end-aligned', span('num', m.reward), span(`ph-only d47-mono ${m.urgent ? 'warn' : ''}`.trim(), m.left)),
            ),
          ),
        ),
      ),
    ),
  ];
}

function shipsPage(doc, argument) {
  const v = shipsView(doc);
  const stored = v.stored;
  const chosen = argument === 'flying' || (argument === null && v.detail) ? 'flying' : Number(argument);
  const shownIndex = chosen === 'flying' ? null : stored[chosen] ? chosen : stored.length ? 0 : null;
  const showFlown = chosen === 'flying' || (shownIndex === null && v.detail);
  const ship = shownIndex === null ? null : stored[shownIndex];

  const list = h(
    'nav',
    { class: 'd47-list', 'aria-label': 'Ships' },
    v.detail
      ? [
          div('d47-list-head', 'Flying now'),
          h(
            'a',
            { class: `d47-list-row ${showFlown ? 'selected' : ''}`.trim(), href: routeHref('ships', 'flying') },
            div('d47-list-row__main', span('d47-list-row__name', v.flownLabel), span('d47-list-row__sub', v.flownWhere)),
          ),
        ]
      : null,
    stored.length ? div('d47-list-head', 'Stored') : null,
    stored.map((s, i) =>
      h(
        'a',
        { class: `d47-list-row ${!showFlown && i === shownIndex ? 'selected' : ''}`.trim(), href: routeHref('ships', i) },
        div('d47-list-row__main', span('d47-list-row__name', s.label), span('d47-list-row__sub', s.where)),
        s.value ? span('d47-list-row__aside d47-mono', s.value) : null,
      ),
    ),
  );

  let detail = null;
  if (showFlown) {
    const d = v.detail;
    detail = div(
      'web__stack',
      div('web__section', titleBlock(d.context, d.title, 'Rebuy', d.rebuy), d.tiles.length ? div('d47-data-grid', d.tiles.map((t) => tile({ ...t, mono: true }))) : null),
      d.modules.length
        ? div(
            'web__section',
            sectionHead('As it is fitted', 'As your last loadout read it.'),
            div(
              'wt web__modules',
              div(
                'd47-sortrow',
                div('d47-sorthead', span('d47-sorthead__label', 'Module'), span('d47-sorthead__second', 'Slot')),
                div('d47-sorthead', span('d47-sorthead__label', 'Engineering'), span('d47-sorthead__second', 'Experimental')),
                div('d47-sorthead right', span('d47-sorthead__label', 'Health')),
              ),
              d.modules.map((m) =>
                div(
                  'wrow',
                  div('two', span('name', m.item), span('', m.slot)),
                  div('two ph-line', span(`chip ${m.engineered ? '' : 'web__grey'}`.trim(), m.engineering), span('', m.experimental)),
                  span('num', m.health),
                ),
              ),
            ),
          )
        : null,
    );
  } else if (ship) {
    detail = div(
      'web__stack',
      div('web__section', titleBlock('Stored ship', ship.label, 'Value', ship.value || null), div('d47-data-grid', tile({ label: 'Where it is', value: ship.where, tone: 'white small', wide: true }))),
      hint("A stored ship's fittings are not in the snapshot."),
    );
  }

  return div('split', list, detail);
}

function carrierPage(doc, now) {
  const v = carrierView(doc, now);
  return [
    div(
      'web__section',
      titleBlock(v.context, v.title, 'Carrier balance', v.balance),
      v.tiles.length
        ? div(
            'd47-data-grid',
            v.tiles.map((t) => (t.gauge !== undefined ? gaugeTile({ label: t.label, value: t.value, pct: t.gauge }) : tile(t))),
          )
        : null,
    ),
    v.tritium.length
      ? div(
          'web__section',
          sectionHead('Tritium'),
          div('d47-data-grid', v.tritium.map((t) => (t.gauge !== undefined ? gaugeTile({ label: t.label, value: t.value, pct: t.gauge }) : tile(t)))),
        )
      : null,
    v.open || v.off
      ? div(
          'web__section',
          sectionHead('Services'),
          div(
            'd47-data-grid',
            v.open ? tile({ label: 'Open', value: v.open, wide: true }) : null,
            v.off ? tile({ label: 'Switched off', value: v.off, tone: 'web__grey' }) : null,
          ),
        )
      : null,
  ];
}

function engineersPage(doc, argument) {
  const v = engineersView(doc, argument);
  const d = v.detail;
  return div(
    'split',
    h('nav', { class: 'd47-list', 'aria-label': 'Engineers' }, listRows(v.groups, 'engineers')),
    d
      ? div(
          'web__stack',
          div('web__section', titleBlock(d.context, d.title), div('d47-data-grid', d.tiles.map(tile))),
          d.prerequisites.length
            ? div(
                'web__section',
                sectionHead('Unlock prerequisites'),
                ladder(d.prerequisites),
                hint('Unknown means the journal had not said yet when the snapshot was taken.'),
              )
            : null,
        )
      : null,
  );
}

function materialsPage(doc, argument) {
  const v = materialsView(doc, argument);
  return [
    h(
      'nav',
      { class: 'd47-segmented cols-3', 'aria-label': 'Material kinds' },
      v.kinds.map((k) =>
        h(
          'a',
          { class: `d47-segment has-status ${k.category === v.chosen ? 'selected' : ''}`.trim(), href: routeHref('materials', k.category) },
          span('d47-segment__label', k.category),
          span('d47-segment__status', k.status),
        ),
      ),
    ),
    v.grades.map((g) =>
      div(
        'web__section',
        sectionHead(g.title, g.cap),
        div(
          'd47-data-grid four',
          g.items.map((m) =>
            div(
              'd47-data web__tight',
              div('d47-data__row', span('d47-data__label web__white', m.name), span('d47-data__value yellow small d47-data__end d47-mono', m.count)),
              m.pct === null ? null : gauge(m.pct, 'capacity'),
            ),
          ),
        ),
      ),
    ),
  ];
}

function planPage(doc, argument) {
  const v = planView(doc, argument);
  const d = v.detail;
  const route = d?.route;
  return div(
    'split',
    h('nav', { class: 'd47-list', 'aria-label': 'Route plans' }, div('d47-list-head', 'Route plans'), listRows([{ title: null, rows: v.rows }], 'plan').map((x) => x.slice(1))),
    d
      ? div(
          'web__stack',
          div(
            'web__section',
            titleBlock(d.context, d.title),
            route
              ? div(
                  'd47-data-grid',
                  tile({ label: 'Distance', value: route.distance, mono: true }),
                  tile({ label: 'Jumps', value: route.jumps, mono: true }),
                  route.reached
                    ? div('d47-data web__tight', div('d47-data__row', span('d47-data__label', 'Reached'), span('d47-data__value small d47-data__end d47-mono', route.reached)), gauge(route.reachedPct))
                    : null,
                )
              : hint('The stops of this kind of plan are drawn in D47 on your PC.'),
          ),
          route && route.rows.length
            ? div(
                'web__section',
                sectionHead('Waypoints', String(route.total)),
                div(
                  'wt web__waypoints',
                  div(
                    'd47-sortrow',
                    div('d47-sorthead right', span('d47-sorthead__label', '#')),
                    div('d47-sorthead', span('d47-sorthead__label', 'System')),
                    div('d47-sorthead right', span('d47-sorthead__label', 'Jumps')),
                    div('d47-sorthead right', span('d47-sorthead__label', 'Distance left')),
                    div('d47-sorthead', span('d47-sorthead__label', 'Star')),
                  ),
                  route.rows.map((w) =>
                    div(
                      `wrow ${w.reached ? 'faded' : ''}`.trim(),
                      span('num ph-hide web__grey', String(w.n)),
                      div('two', span('sys', w.system), span('ph-only', w.phone)),
                      span('num ph-hide', w.jumps),
                      span('num', w.left),
                      span(`chip ph-hide ${w.neutron ? '' : 'web__grey'}`.trim(), w.neutron ? 'Neutron' : '—'),
                    ),
                  ),
                  route.more > 0 ? div('d47-hint web__more', `${route.more} more ${route.more === 1 ? 'waypoint' : 'waypoints'} to ${route.destination}.`) : null,
                ),
              )
            : null,
        )
      : null,
  );
}

function accountPage(doc, now, ui) {
  const age = ageView(doc.takenAt, now);
  return [
    div(
      'web__section web__narrow',
      div(
        'd47-data-grid',
        tile({ label: 'Signed in as', value: whoOf({ kind: 'snapshot', doc }), tone: 'white' }),
        tile({ label: 'Snapshot taken', value: age ? age.taken.toUpperCase() : '—', tone: 'small', mono: true }),
        tile({ label: 'Uploads', value: 'ON IN D47', tone: 'small' }),
      ),
    ),
    div(
      'web__section web__narrow',
      sectionHead('Sign out'),
      div(
        'web__actions',
        h('p', { class: 'web__p' }, 'Signs this browser out. The snapshot stays, and you see it again when you sign in.'),
        signOutButton(ui),
      ),
    ),
    div(
      'web__section web__narrow',
      sectionHead('Delete the snapshot'),
      div(
        'web__actions',
        h(
          'p',
          { class: 'web__p' },
          'Deletes the snapshot D47 stores for this Commander and signs you out. D47 on your PC keeps its own data, and uploads again unless you turn uploads off there.',
        ),
        h('button', { class: 'd47-tile-button tall destructive', type: 'button', on: { click: ui.askDelete } }, 'Delete'),
      ),
    ),
  ];
}

function deleteDialog(doc, ui) {
  const buttons = [
    h('button', { class: 'd47-tile-button tall', type: 'button', on: { click: ui.keep } }, 'Keep it'),
    h('button', { class: 'd47-tile-button tall destructive', type: 'button', on: { click: ui.confirmDelete } }, 'Delete'),
  ];
  return div(
    'd47-scrim',
    h(
      'div',
      { class: 'd47-modal', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': 'del-title' },
      div('d47-modal__head', div('d47-title-block__text', span('d47-modal__dek', whoOf({ kind: 'snapshot', doc })), h('span', { class: 'd47-modal__title', id: 'del-title' }, 'Delete the snapshot'))),
      div('d47-modal__body', h('p', { class: 'web__p' }, 'This cannot be undone. The web page shows nothing for this Commander until D47 on your PC uploads again.')),
      div('d47-modal__foot', buttons),
    ),
  );
}

// ---------------------------------------------------------------------------------------------
// Whole screens

function signedOut() {
  return [
    h('header', { class: 'web__bar' }, span('d47-brand-mark'), span('d47-brand-name', 'DIRECTIVE 47')),
    h(
      'main',
      { class: 'web__body web__center' },
      div(
        'web__card',
        div('d47-title-block', div('d47-title-block__text', span('d47-title-block__context', SIGNED_OUT.context), h('h1', { class: 'd47-title web__flush' }, SIGNED_OUT.title))),
        h('p', { class: 'web__lead' }, SIGNED_OUT.lead),
        h('a', { class: 'd47-tile-button full web__wide', href: '/signin' }, 'Sign in with Frontier'),
        div('d47-data prose', span('d47-data__label', 'What sign-in shares'), span('web__prose', SIGNED_OUT.shares)),
        span('d47-hint web__hint', SIGNED_OUT.hint),
      ),
    ),
  ];
}

function emptyState(state, now, ui) {
  const v = emptyStateView(state);
  return [
    bar(state, now, () => {}, ui),
    h(
      'main',
      { class: 'web__body web__top' },
      div(
        'web__section web__state',
        div('d47-title-block', div('d47-title-block__text', span('d47-title-block__context', v.context), h('h1', { class: 'd47-title web__flush' }, v.title))),
        h('p', { class: 'web__lead' }, v.lead),
        v.detail ? div('d47-notice warning', div('d47-notice__main', span('d47-notice__label', 'Uploads turned off'), span('d47-notice__detail', v.detail))) : null,
        v.what ? div('d47-data prose', span('d47-data__label', 'What arrives'), span('web__prose', v.what)) : null,
        v.notice
          ? div(
              'd47-notice warning',
              div('d47-notice__main', span('d47-notice__label', v.notice.label), span('d47-notice__text', v.notice.text)),
              div('d47-notice__actions', signOutButton(ui)),
            )
          : null,
        v.hint ? span('d47-hint web__hint', v.hint) : null,
      ),
    ),
  ];
}

function snapshotScreen(state, route, now, ui) {
  const doc = state.doc;
  const page = route.page;
  const age = ageView(doc.takenAt, now);
  const body = !hasSection(doc, page)
    ? absent(page)
    : {
        progress: () => progressPage(doc),
        checklist: () => checklistPage(doc, route.argument),
        watches: () => watchesPage(doc, route.argument),
        missions: () => missionsPage(doc, now),
        ships: () => shipsPage(doc, route.argument),
        carrier: () => carrierPage(doc, now),
        engineers: () => engineersPage(doc, route.argument),
        materials: () => materialsPage(doc, route.argument),
        plan: () => planPage(doc, route.argument),
        account: () => accountPage(doc, now, ui),
      }[page]();

  return [
    bar(state, now, (change) => ui.setMenu(change.menu), { ...ui, page }),
    ui.menuOpen ? menuPanel({ ...ui, page }) : null,
    h(
      'main',
      { class: 'web__body' },
      header(doc, page, now),
      age ? span(`web__age-line ${age.old ? 'old' : ''}`.trim(), age.line) : null,
      body,
    ),
    ui.confirming && page === 'account' ? deleteDialog(doc, ui) : null,
  ];
}

/** Everything inside the page root for this state. */
export function screen(state, route, now, ui) {
  const nodes =
    state.kind === 'signedOut'
      ? signedOut()
      : state.kind === 'snapshot'
        ? snapshotScreen(state, route, now, ui)
        : emptyState(state, now, ui);
  return [...nodes, div('d47-scanlines')].filter(Boolean);
}
