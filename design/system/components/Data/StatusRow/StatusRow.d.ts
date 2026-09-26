import * as React from 'react';

/**
 * StatusRow props.
 */
export interface StatusRowProps {
  /** Cyan status text beside the dot, e.g. "PTT ready". */
  status?: string;
  /** Session cost, e.g. "$0.1432". */
  cost?: string;
  onSpend?: () => void;
}

export declare function StatusRow(props: StatusRowProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const StatusRowClasses: {
  readonly 'status': 'd47-status';
  readonly 'composer': 'd47-composer';
};
