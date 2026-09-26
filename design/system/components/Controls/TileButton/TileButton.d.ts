import * as React from 'react';

/**
 * TileButton props.
 */
export interface TileButtonProps {
  children?: React.ReactNode;
  /** No button weight: default or destructive, nothing else. */
  variant?: 'default' | 'destructive';
  /** tall 40px (settings rows), full 44px (beside a field), compact 28px (status row). */
  size?: 'default' | 'tall' | 'full' | 'compact';
  selected?: boolean;
  disabled?: boolean;
  onClick?: () => void;
  style?: React.CSSProperties;
}

export declare function TileButton(props: TileButtonProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const TileButtonClasses: {
  readonly 'tile-button': 'd47-tile-button';
  readonly 'destructive': 'destructive';
  readonly 'tall': 'tall';
  readonly 'full': 'full';
  readonly 'compact': 'compact';
  readonly 'selected': 'selected';
  readonly 'is-pressed': 'is-pressed';
};
