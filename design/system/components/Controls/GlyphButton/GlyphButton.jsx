// GlyphButton — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function GlyphButton({ glyph = 'copy', label, onClick, disabled }) {
  return (
    <span role="button" tabIndex={disabled ? -1 : 0} aria-label={label} className={cx('d47-glyph', disabled && 'disabled')}
      onClick={() => !disabled && onClick && onClick()}>
      {label && !disabled && <span className="d47-glyph__tip">{label}</span>}
      <span className="d47-glyph__face">{glyph === 'copy' ? <span className="d47-copy-icon"></span> : glyph}</span>
    </span>
  );
}

export const GlyphButtonClasses = {
  "glyph": "d47-glyph",
  "copy-icon": "d47-copy-icon",
  "disabled": "disabled"
};
