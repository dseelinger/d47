import * as React from 'react';

/**
 * GlyphButton props.
 */
export interface GlyphButtonProps {
  /** "copy" draws the copy icon;
  any other string is drawn as the glyph (e.g. "\u21BA"). */ glyph?: string;
  /** The hover label: what it does. */
  label?: string;
  onClick?: () => void;
  disabled?: boolean;
}

export declare function GlyphButton(props: GlyphButtonProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const GlyphButtonClasses: {
  readonly 'glyph': 'd47-glyph';
  readonly 'copy-icon': 'd47-copy-icon';
  readonly 'disabled': 'disabled';
};
