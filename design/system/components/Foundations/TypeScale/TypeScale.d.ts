import * as React from 'react';

/**
 * TypeScale props.
 */
export interface TypeScaleProps {
  /** Override the sample strings. */
  samples?: Partial<Record<'title' | 'tab' | 'group' | 'label' | 'body' | 'control' | 'meta', string>>;
}

export declare function TypeScale(props: TypeScaleProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const TypeScaleClasses: {
  readonly 'title': 'd47-title';
  readonly 'chrome': 'd47-chrome';
  readonly 'mono': 'd47-mono';
};
