import * as React from 'react';

/**
 * ListBoxItem props.
 */
export interface ListBoxItemProps {
  name: string;
  sub?: string;
  aside?: string;
  selected?: boolean;
  disabled?: boolean;
  /** 36px row. */
  compact?: boolean;
  onClick?: () => void;
}

export declare function ListBoxItem(props: ListBoxItemProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const ListBoxItemClasses: {
  readonly 'list': 'd47-list';
  readonly 'list-head': 'd47-list-head';
  readonly 'list-row': 'd47-list-row';
};
