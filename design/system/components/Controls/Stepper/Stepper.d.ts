import * as React from 'react';

/**
 * Stepper props.
 */
export interface StepperProps {
  options: Array<string | { value: string; label: string }>;
  value?: string;
  onChange?: (value: string) => void;
  /** Grey text: the value follows another setting. */
  inherited?: boolean;
  /** Wrap from last to first. Default true. */
  wrap?: boolean;
  style?: React.CSSProperties;
}

export declare function Stepper(props: StepperProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const StepperClasses: {
  readonly 'stepper': 'd47-stepper';
};
