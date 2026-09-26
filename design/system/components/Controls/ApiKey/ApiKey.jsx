// ApiKey — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function ApiKey({ last4 = '', stored = true, status, placeholder = 'Paste the new key', onSave, onVerify, style }) {
  const [editing, setEditing] = React.useState(false);
  const [draft, setDraft] = React.useState('');
  if (editing) {
    return (
      <div className="d47-apikey" style={style}>
        <input className="d47-apikey__input" autoFocus placeholder={placeholder} value={draft} onChange={e => setDraft(e.target.value)} />
        <button className="d47-tile-button tall" onClick={() => { onSave && onSave(draft); setDraft(''); setEditing(false); }}>Save</button>
        <button className="d47-tile-button tall" onClick={() => { setDraft(''); setEditing(false); }}>Cancel</button>
      </div>
    );
  }
  return (
    <div className="d47-apikey" style={style}>
      <div className="d47-apikey__stored">
        <span className="d47-apikey__mask">{stored ? '\u2022'.repeat(8) + last4 : '\u2014'}</span>
        <span className={cx('d47-apikey__status', !stored && 'missing')}>{status || (stored ? 'Key stored' : 'No key')}</span>
      </div>
      <button className="d47-tile-button tall" onClick={() => setEditing(true)}>Replace</button>
      <button className="d47-tile-button tall" disabled={!stored} onClick={onVerify}>Verify</button>
    </div>
  );
}

export const ApiKeyClasses = {
  "apikey_stored": "d47-apikey__stored",
  "apikey_input": "d47-apikey__input"
};
