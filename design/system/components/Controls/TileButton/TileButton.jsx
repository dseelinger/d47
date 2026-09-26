// TileButton — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function TileButton({ children, variant = 'default', size = 'default', selected, disabled, onClick, style }) {
  return (
    <button className={cx('d47-tile-button', variant === 'destructive' && 'destructive', size !== 'default' && size, selected && 'selected')}
      disabled={disabled} onClick={onClick} style={style}>{children}</button>
  );
}

export const TileButtonClasses = {
  "tile-button": "d47-tile-button",
  "destructive": "destructive",
  "tall": "tall",
  "full": "full",
  "compact": "compact",
  "selected": "selected",
  "is-pressed": "is-pressed"
};
