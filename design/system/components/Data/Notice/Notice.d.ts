import * as React from 'react';

/**
 * Notice props.
 */
export interface NoticeProps {
  /** error: something failed or is blocked (red). warning: a caution the Commander can act on (amber). */
  level?: 'error' | 'warning';
  /** Chrome label. Defaults to "Error" / "Warning". Prefer what failed: "Speech engine offline". */
  label?: string;
  /** One or two plain sentences: what happened, then what to do. */
  children?: React.ReactNode;
  /** Mono detail line, e.g. a provider code "HTTP 401 · GROQ". */
  detail?: string;
  /** Tile buttons at the right, e.g. RETRY. */
  actions?: React.ReactNode;
  /** Tighter padding, for use inside a settings row or under a control. */
  inline?: boolean;
  style?: React.CSSProperties;
}

export declare function Notice(props: NoticeProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const NoticeClasses: {
  readonly 'notice': 'd47-notice';
  readonly 'warning': 'warning';
  readonly 'inline': 'inline';
};
