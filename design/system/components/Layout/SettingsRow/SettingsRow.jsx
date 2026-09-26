// SettingsRow — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
import { GlyphButton } from '../../Controls/GlyphButton/GlyphButton.jsx';
const cx = (...a) => a.filter(Boolean).join(' ');

export function SettingsRow({ label, children, protected: prot, dirty, onReset }) {
  return (
    <div className={cx('d47-row', prot && 'protected')}>
      <span className="d47-row__label">{label}</span>
      <div className="d47-row__control">{children}</div>
      <span className="d47-row__reset">{dirty && <GlyphButton glyph="\u21BA" label="Reset to default" onClick={onReset} />}</span>
    </div>
  );
}

export const SettingsRowClasses = {
  "settings": "d47-settings",
  "row": "d47-row",
  "protected": "protected",
  "inset": "d47-inset"
};
