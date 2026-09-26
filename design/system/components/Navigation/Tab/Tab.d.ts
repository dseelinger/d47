import * as React from 'react';

/**
 * Tab props.
 */
export interface TabProps {
  tabs: Array<string | { value: string; label: string }>;
  value?: string;
  onChange?: (value: string) => void;
  /** tabs: tile strip on a 2px rule. subtabs: text with a 2px underline. */
  variant?: 'tabs' | 'subtabs';
}

export declare function Tab(props: TabProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const TabClasses: {
  readonly 'tabs': 'd47-tabs';
  readonly 'tab': 'd47-tab';
  readonly 'active': 'active';
  readonly 'subtabs': 'd47-subtabs';
  readonly 'subtab': 'd47-subtab';
  readonly 'subbar': 'd47-subbar';
  readonly 'sidebar': 'd47-sidebar';
};
