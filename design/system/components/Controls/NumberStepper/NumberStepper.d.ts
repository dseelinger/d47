import * as React from 'react';

/**
 * NumberStepper props.
 */
export interface NumberStepperProps {
  value: number;
  onChange?: (value: number) => void;
  step?: number;
  min?: number;
  max?: number;
  /** Formats the shown value, e.g. v => v + " MS". */
  format?: (value: number) => string;
}

export declare function NumberStepper(props: NumberStepperProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const NumberStepperClasses: {
  readonly 'number-stepper': 'd47-number-stepper';
};
