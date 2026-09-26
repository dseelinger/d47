// GroupHead — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
import { GlyphButton } from '../../Controls/GlyphButton/GlyphButton.jsx';

export function GroupHead({ name, description, dirty, onReset }) {
  return (
    <div className="d47-group-head">
      <span className="d47-group-head__name">{name}</span>
      {description && <span className="d47-group-head__desc">{description}</span>}
      <span className="d47-group-head__reset">{dirty && <GlyphButton glyph="\u21BA" label="Reset to default" onClick={onReset} />}</span>
    </div>
  );
}

export const GroupHeadClasses = {
  "page-head": "d47-page-head",
  "group-head": "d47-group-head"
};
