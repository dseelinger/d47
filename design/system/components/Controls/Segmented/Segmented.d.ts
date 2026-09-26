import * as React from 'react';

/**
 * Segmented props.
 */
export interface SegmentedProps {
  options: Array<{ value: string; label: string; /** 11px second line. */ status?: string; /** Status in yellow (brown when selected). */ stored?: boolean }>;
  value?: string;
  onChange?: (value: string) => void;
  /** Four columns by default; 2 for a 2×2 of long labels. */
  cols?: 2 | 3 | 4;
  style?: React.CSSProperties;
}

export declare function Segmented(props: SegmentedProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const SegmentedClasses: {
  readonly 'segmented': 'd47-segmented';
  readonly 'cols-2': 'cols-2';
  readonly 'cols-3': 'cols-3';
  readonly 'segment': 'd47-segment';
  readonly 'selected': 'selected';
  readonly 'has-status': 'has-status';
  readonly 'segment_label': 'd47-segment__label';
  readonly 'stored': 'stored';
};
