// DataBlock — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';
const cx = (...a) => a.filter(Boolean).join(' ');

export function DataBlock({ label, value, tone = 'orange', small, span2, prose, gauge, gaugeKind = 'progress' }) {
  if (prose != null) return <div className={cx('d47-data prose', span2 && 'span-2')}><span className="d47-data__label">{label}</span>{prose}</div>;
  const v = <span className={cx('d47-data__value', tone !== 'orange' && tone, small && 'small', gauge != null && 'd47-data__end')}>{value}</span>;
  if (gauge != null) {
    return (
      <div className={cx('d47-data', span2 && 'span-2')} style={{ gap: 5 }}>
        <div className="d47-data__row"><span className="d47-data__label">{label}</span>{v}</div>
        <div className={cx('d47-gauge', gaugeKind !== 'progress' && gaugeKind)}><div className="d47-gauge__fill" style={{ width: Math.min(100, gauge * 100) + '%' }}></div></div>
      </div>
    );
  }
  return <div className={cx('d47-data', span2 && 'span-2')}><span className="d47-data__label">{label}</span>{v}</div>;
}

export const DataBlockClasses = {
  "data-grid": "d47-data-grid",
  "span-2": "span-2",
  "data": "d47-data",
  "gauge": "d47-gauge",
  "capacity": "capacity",
  "over": "over",
  "ladder_state": "d47-ladder__state",
  "met": "met",
  "unknown": "unknown",
  "unmet": "unmet"
};
