import * as React from 'react';

/**
 * ApiKey props.
 */
export interface ApiKeyProps {
  /** Last characters shown after the mask dots, e.g. "a91C". */
  last4?: string;
  stored?: boolean;
  /** Status label. Default "Key stored" / "No key". */
  status?: string;
  placeholder?: string;
  onSave?: (key: string) => void;
  onVerify?: () => void;
  style?: React.CSSProperties;
}

export declare function ApiKey(props: ApiKeyProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const ApiKeyClasses: {
  readonly 'apikey_stored': 'd47-apikey__stored';
  readonly 'apikey_input': 'd47-apikey__input';
};
