// Message — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function Message({ from = 'd47', name, intent, delivery, time, children, chips, cost }) {
  return (
    <div className={cx('d47-message', from === 'cmdr' && 'cmdr')}>
      <div className="d47-message__head">
        <span className="d47-message__name">{name || (from === 'cmdr' ? 'CMDR' : 'D47')}</span>
        {intent && <span className="d47-message__intent">{intent}</span>}
        {delivery && <span className="d47-message__delivery">{delivery}</span>}
        {time && <span className="d47-message__time">{time}</span>}
      </div>
      <div className="d47-message__body">{children}</div>
      {chips && chips.length > 0 && (
        <div className="d47-message__chips">{chips.map((c, i) => <span key={i} className={cx('d47-message__chip', c.here && 'here')}>{c.label}</span>)}</div>
      )}
      {cost && cost.length > 0 && (
        <div className="d47-message__cost">{cost.map((p, i) => (
          <React.Fragment key={i}>{i > 0 && <span>\u00B7</span>}<span className={i === cost.length - 1 ? 'd47-message__cost-value' : undefined}>{p}</span></React.Fragment>
        ))}</div>
      )}
    </div>
  );
}

export const MessageClasses = {
  "messages": "d47-messages",
  "message": "d47-message",
  "cmdr": "cmdr"
};
