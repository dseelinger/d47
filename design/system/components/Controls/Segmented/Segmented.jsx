// Segmented — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function Segmented({ options = [], value, onChange, cols, style }) {
  return (
    <div role="radiogroup" className={cx('d47-segmented', cols === 2 && 'cols-2', cols === 3 && 'cols-3')} style={style}>
      {options.map(o => (
        <div key={o.value} role="radio" aria-checked={o.value === value} tabIndex={0}
          className={cx('d47-segment', o.status && 'has-status', o.value === value && 'selected')}
          onClick={() => onChange && onChange(o.value)}>
          <span className="d47-segment__label">{o.label}</span>
          {o.status && <span className={cx('d47-segment__status', o.stored && 'stored')}>{o.status}</span>}
        </div>
      ))}
    </div>
  );
}

export const SegmentedClasses = {
  "segmented": "d47-segmented",
  "cols-2": "cols-2",
  "cols-3": "cols-3",
  "segment": "d47-segment",
  "selected": "selected",
  "has-status": "has-status",
  "segment_label": "d47-segment__label",
  "stored": "stored"
};
