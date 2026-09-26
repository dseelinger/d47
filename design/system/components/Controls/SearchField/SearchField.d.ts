import * as React from 'react';

/**
 * SearchField props.
 */
export interface SearchFieldProps {
  value?: string;
  onChange?: (value: string) => void;
  placeholder?: string;
}

export declare function SearchField(props: SearchFieldProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const SearchFieldClasses: {
  readonly 'search': 'd47-search';
};
