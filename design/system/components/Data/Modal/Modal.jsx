// Modal — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';

export function Modal({ open = true, dek, title, figureLabel, figureValue, children, footer, onClose, contained }) {
  if (!open) return null;
  return (
    <div className="d47-scrim" style={contained ? undefined : { position: 'fixed' }} onClick={e => { if (e.target === e.currentTarget && onClose) onClose(); }}>
      <div className="d47-modal" role="dialog" aria-label={title}>
        <div className="d47-modal__head">
          <div className="d47-title-block__text">{dek && <span className="d47-modal__dek">{dek}</span>}<span className="d47-modal__title">{title}</span></div>
          {figureValue && <div className="d47-title-block__figure">{figureLabel && <span className="d47-title-block__figure-label">{figureLabel}</span>}<span className="d47-modal__figure-value">{figureValue}</span></div>}
        </div>
        <div className="d47-modal__body">{children}</div>
        <div className="d47-modal__foot">{footer || <button className="d47-tile-button tall" onClick={onClose}>Close</button>}</div>
      </div>
    </div>
  );
}

export const ModalClasses = {
  "scrim": "d47-scrim",
  "modal": "d47-modal",
  "breakdown": "d47-breakdown"
};
