import * as React from 'react';

/**
 * SettingsRow props.
 */
export interface SettingsRowProps {
  label: string;
  children?: React.ReactNode;
  /** Orange 3px left border: D47 won't change it on request. */
  protected?: boolean;
  /** Shows ↺ in the always-reserved reset column. */
  dirty?: boolean;
  onReset?: () => void;
}

export declare function SettingsRow(props: SettingsRowProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const SettingsRowClasses: {
  readonly 'settings': 'd47-settings';
  readonly 'row': 'd47-row';
  readonly 'protected': 'protected';
  readonly 'inset': 'd47-inset';
};
