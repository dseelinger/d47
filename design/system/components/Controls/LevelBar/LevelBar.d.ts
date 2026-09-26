import * as React from 'react';

/**
 * LevelBar props.
 */
export interface LevelBarProps {
  /** 0–1. */
  value: number;
  onChange?: (value: number) => void;
  muted?: boolean;
  segments?: number;
  style?: React.CSSProperties;
}

export declare function LevelBar(props: LevelBarProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const LevelBarClasses: {
  readonly 'level': 'd47-level';
  readonly 'muted': 'muted';
};
