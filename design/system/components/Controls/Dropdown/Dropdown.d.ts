import * as React from 'react';

/**
 * Dropdown props.
 */
export interface DropdownProps {
  options: Array<string | { value: string; label: string }>;
  value?: string;
  onChange?: (value: string) => void;
  placeholder?: string;
  /** Lay the open list in flow rather than over what follows. */
  inline?: boolean;
  defaultOpen?: boolean;
  style?: React.CSSProperties;
}

export declare function Dropdown(props: DropdownProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const DropdownClasses: {
  readonly 'dropdown': 'd47-dropdown';
  readonly 'inline': 'inline';
};
