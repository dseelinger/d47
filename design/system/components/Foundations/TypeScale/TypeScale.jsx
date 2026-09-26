// TypeScale — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';

export function TypeScale({ samples = {} }) {
  const s = Object.assign({ title: 'Sacred Fire', tab: 'Transcript', group: 'Microphone', label: "How D47 knows you're talking to it", body: 'Functioning within tolerance, Commander.', control: 'Add to checklist', meta: '19:42 \u00B7 $0.1075' }, samples);
  const cap = { width: 110, fontFamily: 'var(--d47-font-mono)', fontSize: 11, color: 'var(--d47-grey2)' };
  const row = { display: 'flex', alignItems: 'baseline', gap: 14 };
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={row}><span style={cap}>title 28</span><span className="d47-title">{s.title}</span></div>
      <div style={row}><span style={cap}>tab 15</span><span className="d47-tab active">{s.tab}</span></div>
      <div style={row}><span style={cap}>group 15/600</span><span className="d47-group-head__name">{s.group}</span></div>
      <div style={row}><span style={cap}>row label 15</span><span className="d47-row__label">{s.label}</span></div>
      <div style={row}><span style={cap}>body 16</span><span className="d47-message__body">{s.body}</span></div>
      <div style={row}><span style={cap}>control 13\u201314</span><span className="d47-chrome" style={{ fontSize: 13, fontWeight: 600 }}>{s.control}</span></div>
      <div style={row}><span style={cap}>meta 11\u201312</span><span className="d47-mono" style={{ fontSize: 12, color: 'var(--d47-grey2)' }}>{s.meta}</span></div>
    </div>
  );
}

export const TypeScaleClasses = {
  "title": "d47-title",
  "chrome": "d47-chrome",
  "mono": "d47-mono"
};
