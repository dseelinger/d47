// ListBoxItem — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function ListBoxItem({ name, sub, aside, selected, disabled, compact, onClick }) {
  return (
    <div className={cx('d47-list-row', selected && 'selected', disabled && 'disabled', compact && 'compact')}
      onClick={() => !disabled && onClick && onClick()}>
      <div className="d47-list-row__main">
        <span className="d47-list-row__name">{name}</span>
        {sub && <span className="d47-list-row__sub">{sub}</span>}
      </div>
      {aside && <span className="d47-list-row__aside">{aside}</span>}
    </div>
  );
}

export const ListBoxItemClasses = {
  "list": "d47-list",
  "list-head": "d47-list-head",
  "list-row": "d47-list-row"
};
