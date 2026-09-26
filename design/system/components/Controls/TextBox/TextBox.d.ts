import * as React from 'react';

/**
 * TextBox props.
 */
export interface TextBoxProps {
  /** Channel prefix, e.g. "TO [D47]:". */
  prefix?: string;
  value?: string;
  onChange?: (value: string) => void;
  onSubmit?: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  /** Red outline and a red one-line message under the field. Takes precedence over warning. */
  error?: string;
  /** Amber outline and message: a caution, not a failure. */
  warning?: string;
  style?: React.CSSProperties;
}

export declare function TextBox(props: TextBoxProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const TextBoxClasses: {
  readonly 'field': 'd47-field';
  readonly 'invalid': 'invalid';
  readonly 'warning': 'warning';
  readonly 'field-wrap': 'd47-field-wrap';
  readonly 'field-message': 'd47-field-message';
};
