import * as React from 'react';

/**
 * DataBlock props.
 */
export interface DataBlockProps {
  label: string;
  value?: React.ReactNode;
  /** orange (a value), white (a name), cyan (here), yellow (stored). */
  tone?: 'orange' | 'white' | 'cyan' | 'yellow';
  small?: boolean;
  /** Span two columns of a .d47-data-grid. */
  span2?: boolean;
  /** A block of grey read-only prose instead of a value. */
  prose?: React.ReactNode;
  /** 0–1 gauge under the value. */
  gauge?: number;
  gaugeKind?: 'progress' | 'capacity' | 'over';
}

export declare function DataBlock(props: DataBlockProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const DataBlockClasses: {
  readonly 'data-grid': 'd47-data-grid';
  readonly 'span-2': 'span-2';
  readonly 'data': 'd47-data';
  readonly 'gauge': 'd47-gauge';
  readonly 'capacity': 'capacity';
  readonly 'over': 'over';
  readonly 'ladder_state': 'd47-ladder__state';
  readonly 'met': 'met';
  readonly 'unknown': 'unknown';
  readonly 'unmet': 'unmet';
};
