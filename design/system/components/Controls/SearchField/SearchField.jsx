// SearchField — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';

export function SearchField({ value, onChange, placeholder = 'Search this page' }) {
  return (
    <div className="d47-search">
      <input value={value} placeholder={placeholder} onChange={e => onChange && onChange(e.target.value)} />
    </div>
  );
}

export const SearchFieldClasses = {
  "search": "d47-search"
};
