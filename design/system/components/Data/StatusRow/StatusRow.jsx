// StatusRow — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';

export function StatusRow({ status = 'PTT ready', cost, onSpend }) {
  return (
    <div className="d47-status">
      <span className="d47-status__dot"></span>
      <span className="d47-status__text">{status}</span>
      {cost && <span className="d47-status__session">Session <span className="d47-status__cost">{cost}</span></span>}
      <button className="d47-tile-button compact" onClick={onSpend}>Spend</button>
    </div>
  );
}

export const StatusRowClasses = {
  "status": "d47-status",
  "composer": "d47-composer"
};
