import * as React from 'react';

/**
 * GroupHead props.
 */
export interface GroupHeadProps {
  name: string;
  description?: string;
  /** Shows the group ↺ in the always-reserved slot. */
  dirty?: boolean;
  onReset?: () => void;
}

export declare function GroupHead(props: GroupHeadProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const GroupHeadClasses: {
  readonly 'page-head': 'd47-page-head';
  readonly 'group-head': 'd47-group-head';
};
