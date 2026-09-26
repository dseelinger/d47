// Stepper — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function Stepper({ options = [], value, onChange, inherited, wrap = true, style }) {
  const opts = options.map(o => typeof o === 'string' ? { value: o, label: o } : o);
  const i = Math.max(0, opts.findIndex(o => o.value === value));
  const n = opts.length;
  const go = d => { let j = i + d; if (wrap) j = (j + n) % n; else j = Math.min(n - 1, Math.max(0, j)); onChange && onChange(opts[j].value); };
  return (
    <div className="d47-stepper" style={style}>
      <button className="d47-stepper__arrow" aria-label="Previous" onClick={() => go(-1)}>\u25C4</button>
      <div className="d47-stepper__value">
        <span className={cx('d47-stepper__text', inherited && 'inherited')}>{opts[i] ? opts[i].label : ''}</span>
        <span className="d47-stepper__count">{n ? (i + 1) + ' / ' + n : ''}</span>
      </div>
      <button className="d47-stepper__arrow" aria-label="Next" onClick={() => go(1)}>\u25BA</button>
    </div>
  );
}

export const StepperClasses = {
  "stepper": "d47-stepper"
};
