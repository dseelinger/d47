import * as React from 'react';

/**
 * Surfaces props.
 */
export interface SurfacesProps {
  /** Brand name in the title bar. */
  brand?: string;
  version?: string;
  children?: React.ReactNode;
  /** Draw the scanline overlay (off in Dark and Light via tokens). Default true. */
  scanlines?: boolean;
  showControls?: boolean;
  style?: React.CSSProperties;
  bodyStyle?: React.CSSProperties;
}

export declare function Surfaces(props: SurfacesProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const SurfacesClasses: {
  readonly 'window': 'd47-window';
  readonly 'titlebar': 'd47-titlebar';
  readonly 'window_body': 'd47-window__body';
  readonly 'title-block': 'd47-title-block';
  readonly 'section-head': 'd47-section-head';
  readonly 'scanlines': 'd47-scanlines';
};
