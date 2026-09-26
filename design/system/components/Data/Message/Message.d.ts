import * as React from 'react';

/**
 * Message props.
 */
export interface MessageProps {
  from?: 'd47' | 'cmdr';
  /** Header name. Commander names in full. */
  name?: string;
  /** Intent tag, e.g. "continuity.resume". */
  intent?: string;
  /** Delivery tag, e.g. "calm". */
  delivery?: string;
  time?: string;
  children?: React.ReactNode;
  chips?: Array<{ label: string; here?: boolean }>;
  /** Mono cost line parts;
  the last is drawn as the value. */ cost?: string[];
}

export declare function Message(props: MessageProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const MessageClasses: {
  readonly 'messages': 'd47-messages';
  readonly 'message': 'd47-message';
  readonly 'cmdr': 'cmdr';
};
