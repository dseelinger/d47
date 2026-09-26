import * as React from 'react';

/**
 * MixerTable props.
 */
export interface MixerTableProps {
  channels: Array<{ id: string; name: string; level: number; muted?: boolean; /** null when the channel cannot duck. */ duck?: number | null; /** Shows the ↺ reset. */ dirty?: boolean }>;
  onChange?: (id: string, patch: { level?: number; muted?: boolean; duck?: number }) => void;
  onReset?: (id: string) => void;
  duckLabel?: string;
}

export declare function MixerTable(props: MixerTableProps): JSX.Element;

/** The raw class names, for hand-written markup. */
export declare const MixerTableClasses: {
  readonly 'mixer_head': 'd47-mixer__head';
  readonly 'mixer_row': 'd47-mixer__row';
  readonly 'mixer_mute': 'd47-mixer__mute';
  readonly 'mixer_none': 'd47-mixer__none';
};
