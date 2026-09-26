// Notice — an error or warning message. Renders the d47 kit markup (components/kit.css).
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function Notice({ level = 'error', label, children, detail, actions, inline, style }) {
  const warn = level === 'warning';
  return (
    <div className={cx('d47-notice', warn && 'warning', inline && 'inline')} role={warn ? 'status' : 'alert'} style={style}>
      <div className="d47-notice__main">
        <span className="d47-notice__label">{label || (warn ? 'Warning' : 'Error')}</span>
        {children && <span className="d47-notice__text">{children}</span>}
        {detail && <span className="d47-notice__detail">{detail}</span>}
      </div>
      {actions && <div className="d47-notice__actions">{actions}</div>}
    </div>
  );
}

export const NoticeClasses = {
  "notice": "d47-notice",
  "warning": "warning",
  "inline": "inline"
};
