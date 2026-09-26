import * as React from 'react';

/**
 * Modal props.
 */
export interface ModalProps {
  open?: boolean;
  dek?: string;
  title: string;
  figureLabel?: string;
  figureValue?: string;
  children?: React.ReactNode;
  /** Footer content: tile buttons, a hint. Defaults to a CLOSE tile. */
  footer?: React.ReactNode;
  onClose?: () => void;
  /** Position the scrim inside the nearest positioned ancestor instead of the viewport. */
  contained?: boolean;
}

export declare function Modal(props: ModalProps): JSX.Element | null;

/** The raw class names, for hand-written markup. */
export declare const ModalClasses: {
  readonly 'scrim': 'd47-scrim';
  readonly 'modal': 'd47-modal';
  readonly 'breakdown': 'd47-breakdown';
};
