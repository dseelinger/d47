// Tab — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function Tab({ tabs = [], value, onChange, variant = 'tabs' }) {
  const opts = tabs.map(t => typeof t === 'string' ? { value: t, label: t } : t);
  const sub = variant === 'subtabs';
  const strip = (
    <div role="tablist" className={sub ? 'd47-subtabs' : 'd47-tabs'}>
      {opts.map(o => (
        <span key={o.value} role="tab" tabIndex={0} aria-selected={o.value === value}
          className={cx(sub ? 'd47-subtab' : 'd47-tab', o.value === value && 'active')}
          onClick={() => onChange && onChange(o.value)}>{o.label}</span>
      ))}
    </div>
  );
  return sub ? strip : <div>{strip}<div className="d47-tabs-rule"></div></div>;
}

export const TabClasses = {
  "tabs": "d47-tabs",
  "tab": "d47-tab",
  "active": "active",
  "subtabs": "d47-subtabs",
  "subtab": "d47-subtab",
  "subbar": "d47-subbar",
  "sidebar": "d47-sidebar"
};
