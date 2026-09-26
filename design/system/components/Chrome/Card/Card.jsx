// Card — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function Card({ context, name, meta, selected, onClick, style }) {
  return (
    <div className={cx('d47-card', selected && 'selected')} onClick={onClick} style={style}>
      {context && <span className="d47-card__context">{context}</span>}
      <span className="d47-card__name">{name}</span>
      {meta && <span className="d47-card__meta">{meta}</span>}
    </div>
  );
}

export const CardClasses = {
  "card": "d47-card",
  "selected": "selected"
};
