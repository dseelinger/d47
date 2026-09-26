// LevelBar — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function LevelBar({ value = 0, onChange, muted, segments = 20, style }) {
  const on = Math.round(value * segments);
  return (
    <div className={cx('d47-level', muted && 'muted')} style={style}>
      <div className="d47-level__track">
        {Array.from({ length: segments }, (_, i) => (
          <span key={i} className={cx('d47-level__seg', i < on && 'on')} onClick={() => onChange && onChange((i + 1) / segments)}></span>
        ))}
      </div>
      <span className="d47-level__value">{value.toFixed(2)}</span>
    </div>
  );
}

export const LevelBarClasses = {
  "level": "d47-level",
  "muted": "muted"
};
