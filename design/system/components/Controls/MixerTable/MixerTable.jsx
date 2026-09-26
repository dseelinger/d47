// MixerTable — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
import { LevelBar } from '../LevelBar/LevelBar.jsx';
import { GlyphButton } from '../GlyphButton/GlyphButton.jsx';
const cx = (...a) => a.filter(Boolean).join(' ');

export function MixerTable({ channels = [], onChange, onReset, duckLabel = 'Duck while D47 speaks' }) {
  const set = (id, patch) => onChange && onChange(id, patch);
  return (
    <div style={{ width: '100%' }}>
      <div className="d47-mixer__head"><div>Channel</div><div>Level</div><div style={{ textAlign: 'center' }}>Mute</div><div>{duckLabel}</div><div></div></div>
      {channels.map(c => (
        <div key={c.id} className="d47-mixer__row">
          <div className="d47-mixer__name">{c.name}</div>
          <LevelBar value={c.level} muted={c.muted} onChange={v => set(c.id, { level: v })} />
          <div className="d47-mixer__center">
            <span className="d47-mixer__mute" onClick={() => set(c.id, { muted: !c.muted })}><span className={cx('d47-check', c.muted && 'checked')}></span></span>
          </div>
          {c.duck == null ? <div className="d47-mixer__none">\u2014</div> : <LevelBar value={c.duck} onChange={v => set(c.id, { duck: v })} />}
          <div className="d47-row__reset">{c.dirty && <GlyphButton glyph="\u21BA" label="Reset to default" onClick={() => onReset && onReset(c.id)} />}</div>
        </div>
      ))}
    </div>
  );
}

export const MixerTableClasses = {
  "mixer_head": "d47-mixer__head",
  "mixer_row": "d47-mixer__row",
  "mixer_mute": "d47-mixer__mute",
  "mixer_none": "d47-mixer__none"
};
