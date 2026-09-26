// Surfaces — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.
import React from 'react';

export function Surfaces({ brand = 'DIRECTIVE 47', version, children, scanlines = true, showControls = true, style, bodyStyle }) {
  return (
    <div className="d47-window" style={style}>
      <div className="d47-titlebar">
        <span className="d47-brand-mark"></span><span className="d47-brand-name">{brand}</span>
        {version && <span className="d47-version">{version}</span>}
        {showControls && <div className="d47-window-controls"><span className="d47-window-control">\u2014</span><span className="d47-window-control">\u25A1</span><span className="d47-window-control close">\u2715</span></div>}
      </div>
      <div className="d47-window__body" style={bodyStyle}>{children}</div>
      {scanlines && <div className="d47-scanlines"></div>}
    </div>
  );
}

export const SurfacesClasses = {
  "window": "d47-window",
  "titlebar": "d47-titlebar",
  "window_body": "d47-window__body",
  "title-block": "d47-title-block",
  "section-head": "d47-section-head",
  "scanlines": "d47-scanlines"
};
