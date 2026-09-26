// NumberStepper — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';

export function NumberStepper({ value, onChange, step = 1, min = -Infinity, max = Infinity, format = v => String(v) }) {
  const go = d => { const n = Math.min(max, Math.max(min, +(value + d * step).toFixed(6))); onChange && onChange(n); };
  return (
    <div className="d47-number-stepper">
      <button className="d47-stepper__arrow" aria-label="Decrease" disabled={value <= min} onClick={() => go(-1)}>\u25C4</button>
      <span className="d47-stepper__value">{format(value)}</span>
      <button className="d47-stepper__arrow" aria-label="Increase" disabled={value >= max} onClick={() => go(1)}>\u25BA</button>
    </div>
  );
}

export const NumberStepperClasses = {
  "number-stepper": "d47-number-stepper"
};
