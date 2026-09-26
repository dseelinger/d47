import * as React from 'react';

/**
 * Card props.
 */
export interface CardProps {
  context?: string;
  name: string;
  meta?: string;
  selected?: boolean;
  onClick?: () => void;
  style?: React.CSSProperties;
}

export declare function Card(props: CardProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const CardClasses: {
  readonly 'card': 'd47-card';
  readonly 'selected': 'selected';
};
