// KeyBinding — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function KeyBinding({ binding, onBind, onClear }) {
  return (
    <div className="d47-keybind">
      <span className={cx('d47-keybind__chip', !binding && 'unbound')}>{binding || 'UNBOUND'}</span>
      <button className="d47-tile-button tall" onClick={onBind}>Bind</button>
      <button className="d47-tile-button tall" onClick={onClear} disabled={!binding}>Clear</button>
    </div>
  );
}

export const KeyBindingClasses = {
  "keybind": "d47-keybind",
  "keybind_chip": "d47-keybind__chip",
  "unbound": "unbound"
};
