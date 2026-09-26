// Palette — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';

const ROLES = { bg: 'Window ground.', bar: 'Title bar and modal ground.', slab: 'Read-only data and disabled ground.', white: 'Names, titles and speech.', grey: 'Labels, helper prose, unavailable.', grey2: 'Placeholders, disabled text.', a: 'Accent: can act, and values.', knock: 'Text on a solid accent fill.', brown: 'Labels inside a selected row.', cyan: 'Yours, current or ready.', blue: 'Confirmed or met.', yellow: 'Stored or equipped.', red: 'Destructive, hostile, locked; errors.', warn: 'Warnings: caution.', tile: 'Accent 20% into bg. At rest.', tile2: 'Accent 30% into bg. Hover.', line: 'Accent 55% into bg. Frame.', line2: 'Accent 28% into bg. Separators.' };
export function Palette({ tokens = Object.keys(ROLES) }) {
  return (
    <div className="swatches" style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: '6px 22px' }}>
      {tokens.map(t => (
        <div key={t} style={{ display: 'grid', gridTemplateColumns: '30px 60px minmax(0, 1fr)', alignItems: 'center', gap: 10, fontSize: 12 }}>
          <span style={{ width: 30, height: 18, background: 'var(--d47-' + t + ')', border: '1px solid rgba(128,128,128,0.35)' }}></span>
          <span className="d47-mono" style={{ color: 'var(--d47-white)' }}>{t}</span>
          <span style={{ color: 'var(--d47-grey)' }}>{ROLES[t] || ''}</span>
        </div>
      ))}
    </div>
  );
}

export const PaletteClasses = {};
