import * as React from 'react';

/**
 * CheckboxTile props.
 */
export interface CheckboxTileProps {
  children?: React.ReactNode;
  checked?: boolean;
  onChange?: (checked: boolean) => void;
  /** 36px chrome-labelled filter checkbox. */
  caps?: boolean;
  disabled?: boolean;
  style?: React.CSSProperties;
}

export declare function CheckboxTile(props: CheckboxTileProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const CheckboxTileClasses: {
  readonly 'checkbox': 'd47-checkbox';
  readonly 'checked': 'checked';
  readonly 'caps': 'caps';
  readonly 'checkbox-grid': 'd47-checkbox-grid';
  readonly 'cols-4': 'cols-4';
  readonly 'check': 'd47-check';
};
