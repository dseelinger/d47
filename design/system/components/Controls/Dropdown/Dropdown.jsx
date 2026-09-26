// Dropdown — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function Dropdown({ options = [], value, onChange, placeholder = '', inline, defaultOpen = false, style }) {
  const [open, setOpen] = React.useState(defaultOpen);
  const opts = options.map(o => typeof o === 'string' ? { value: o, label: o } : o);
  const cur = opts.find(o => o.value === value);
  return (
    <div className={cx('d47-dropdown', inline && 'inline')} style={style}>
      <button className="d47-dropdown__button" onClick={() => setOpen(!open)}>
        <span className="d47-dropdown__value">{cur ? cur.label : placeholder}</span>
        <span className="d47-dropdown__caret">\u25BC</span>
      </button>
      {open && (
        <div className="d47-dropdown__list">
          {opts.map(o => (
            <div key={o.value} className={cx('d47-dropdown__option', o.value === value && 'selected')}
              onClick={() => { onChange && onChange(o.value); setOpen(false); }}>{o.label}</div>
          ))}
        </div>
      )}
    </div>
  );
}

export const DropdownClasses = {
  "dropdown": "d47-dropdown",
  "inline": "inline"
};
