// CheckboxTile — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function CheckboxTile({ children, checked, onChange, caps, disabled, style }) {
  return (
    <div role="checkbox" aria-checked={!!checked} aria-disabled={!!disabled} tabIndex={disabled ? -1 : 0}
      className={cx('d47-checkbox', checked && 'checked', caps && 'caps', disabled && 'disabled')} style={style}
      onClick={() => !disabled && onChange && onChange(!checked)}
      onKeyDown={e => { if (!disabled && (e.key === ' ' || e.key === 'Enter')) { e.preventDefault(); onChange && onChange(!checked); } }}>
      <span className="d47-checkbox__box"></span>{children}
    </div>
  );
}

export const CheckboxTileClasses = {
  "checkbox": "d47-checkbox",
  "checked": "checked",
  "caps": "caps",
  "checkbox-grid": "d47-checkbox-grid",
  "cols-4": "cols-4",
  "check": "d47-check"
};
