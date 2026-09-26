import * as React from 'react';

/**
 * KeyBinding props.
 */
export interface KeyBindingProps {
  /** The bound key, e.g. "BUTTON 11". Empty means unbound. */
  binding?: string;
  onBind?: () => void;
  onClear?: () => void;
}

export declare function KeyBinding(props: KeyBindingProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const KeyBindingClasses: {
  readonly 'keybind': 'd47-keybind';
  readonly 'keybind_chip': 'd47-keybind__chip';
  readonly 'unbound': 'unbound';
};
