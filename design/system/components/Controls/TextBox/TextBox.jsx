// TextBox — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function TextBox({ prefix, value, onChange, onSubmit, placeholder, disabled, error, warning, style }) {
  const level = error ? 'invalid' : warning ? 'warning' : null;
  const msg = error || warning;
  const field = (
    <div className={cx('d47-field', disabled && 'disabled', level)} style={msg ? undefined : style}>
      {prefix && <span className="d47-field__prefix">{prefix}</span>}
      <input value={value} placeholder={placeholder} disabled={disabled} aria-invalid={!!error}
        onChange={e => onChange && onChange(e.target.value)}
        onKeyDown={e => { if (e.key === 'Enter' && onSubmit) onSubmit(e.target.value); }} />
    </div>
  );
  if (!msg || typeof msg !== 'string') return field;
  return (
    <div className="d47-field-wrap" style={style}>
      {field}
      <span className={cx('d47-field-message', !error && 'warning')} role={error ? 'alert' : undefined}>{msg}</span>
    </div>
  );
}

export const TextBoxClasses = {
  "field": "d47-field",
  "invalid": "invalid",
  "warning": "warning",
  "field-wrap": "d47-field-wrap",
  "field-message": "d47-field-message"
};
