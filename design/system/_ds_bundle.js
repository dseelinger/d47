/* @ds-bundle: {"format":4,"namespace":"D47DesignSystem_33498e","components":[{"name":"Card","sourcePath":"components/Chrome/Card/Card.jsx"},{"name":"CardClasses","sourcePath":"components/Chrome/Card/Card.jsx"},{"name":"ApiKey","sourcePath":"components/Controls/ApiKey/ApiKey.jsx"},{"name":"ApiKeyClasses","sourcePath":"components/Controls/ApiKey/ApiKey.jsx"},{"name":"CheckboxTile","sourcePath":"components/Controls/CheckboxTile/CheckboxTile.jsx"},{"name":"CheckboxTileClasses","sourcePath":"components/Controls/CheckboxTile/CheckboxTile.jsx"},{"name":"Dropdown","sourcePath":"components/Controls/Dropdown/Dropdown.jsx"},{"name":"DropdownClasses","sourcePath":"components/Controls/Dropdown/Dropdown.jsx"},{"name":"GlyphButton","sourcePath":"components/Controls/GlyphButton/GlyphButton.jsx"},{"name":"GlyphButtonClasses","sourcePath":"components/Controls/GlyphButton/GlyphButton.jsx"},{"name":"KeyBinding","sourcePath":"components/Controls/KeyBinding/KeyBinding.jsx"},{"name":"KeyBindingClasses","sourcePath":"components/Controls/KeyBinding/KeyBinding.jsx"},{"name":"LevelBar","sourcePath":"components/Controls/LevelBar/LevelBar.jsx"},{"name":"LevelBarClasses","sourcePath":"components/Controls/LevelBar/LevelBar.jsx"},{"name":"ListBoxItem","sourcePath":"components/Controls/ListBoxItem/ListBoxItem.jsx"},{"name":"ListBoxItemClasses","sourcePath":"components/Controls/ListBoxItem/ListBoxItem.jsx"},{"name":"MixerTable","sourcePath":"components/Controls/MixerTable/MixerTable.jsx"},{"name":"MixerTableClasses","sourcePath":"components/Controls/MixerTable/MixerTable.jsx"},{"name":"NumberStepper","sourcePath":"components/Controls/NumberStepper/NumberStepper.jsx"},{"name":"NumberStepperClasses","sourcePath":"components/Controls/NumberStepper/NumberStepper.jsx"},{"name":"SearchField","sourcePath":"components/Controls/SearchField/SearchField.jsx"},{"name":"SearchFieldClasses","sourcePath":"components/Controls/SearchField/SearchField.jsx"},{"name":"Segmented","sourcePath":"components/Controls/Segmented/Segmented.jsx"},{"name":"SegmentedClasses","sourcePath":"components/Controls/Segmented/Segmented.jsx"},{"name":"Stepper","sourcePath":"components/Controls/Stepper/Stepper.jsx"},{"name":"StepperClasses","sourcePath":"components/Controls/Stepper/Stepper.jsx"},{"name":"TextBox","sourcePath":"components/Controls/TextBox/TextBox.jsx"},{"name":"TextBoxClasses","sourcePath":"components/Controls/TextBox/TextBox.jsx"},{"name":"TileButton","sourcePath":"components/Controls/TileButton/TileButton.jsx"},{"name":"TileButtonClasses","sourcePath":"components/Controls/TileButton/TileButton.jsx"},{"name":"DataBlock","sourcePath":"components/Data/DataBlock/DataBlock.jsx"},{"name":"DataBlockClasses","sourcePath":"components/Data/DataBlock/DataBlock.jsx"},{"name":"Message","sourcePath":"components/Data/Message/Message.jsx"},{"name":"MessageClasses","sourcePath":"components/Data/Message/Message.jsx"},{"name":"Modal","sourcePath":"components/Data/Modal/Modal.jsx"},{"name":"ModalClasses","sourcePath":"components/Data/Modal/Modal.jsx"},{"name":"Notice","sourcePath":"components/Data/Notice/Notice.jsx"},{"name":"NoticeClasses","sourcePath":"components/Data/Notice/Notice.jsx"},{"name":"StatusRow","sourcePath":"components/Data/StatusRow/StatusRow.jsx"},{"name":"StatusRowClasses","sourcePath":"components/Data/StatusRow/StatusRow.jsx"},{"name":"Palette","sourcePath":"components/Foundations/Palette/Palette.jsx"},{"name":"PaletteClasses","sourcePath":"components/Foundations/Palette/Palette.jsx"},{"name":"Surfaces","sourcePath":"components/Foundations/Surfaces/Surfaces.jsx"},{"name":"SurfacesClasses","sourcePath":"components/Foundations/Surfaces/Surfaces.jsx"},{"name":"TypeScale","sourcePath":"components/Foundations/TypeScale/TypeScale.jsx"},{"name":"TypeScaleClasses","sourcePath":"components/Foundations/TypeScale/TypeScale.jsx"},{"name":"GroupHead","sourcePath":"components/Layout/GroupHead/GroupHead.jsx"},{"name":"GroupHeadClasses","sourcePath":"components/Layout/GroupHead/GroupHead.jsx"},{"name":"SettingsRow","sourcePath":"components/Layout/SettingsRow/SettingsRow.jsx"},{"name":"SettingsRowClasses","sourcePath":"components/Layout/SettingsRow/SettingsRow.jsx"},{"name":"Tab","sourcePath":"components/Navigation/Tab/Tab.jsx"},{"name":"TabClasses","sourcePath":"components/Navigation/Tab/Tab.jsx"}],"sourceHashes":{"components/Chrome/Card/Card.jsx":"566d87637093","components/Controls/ApiKey/ApiKey.jsx":"db7e84a70c60","components/Controls/CheckboxTile/CheckboxTile.jsx":"45c68c136254","components/Controls/Dropdown/Dropdown.jsx":"3e3d084d4757","components/Controls/GlyphButton/GlyphButton.jsx":"0bdc50c14306","components/Controls/KeyBinding/KeyBinding.jsx":"087415cb07f9","components/Controls/LevelBar/LevelBar.jsx":"8abaee025bfe","components/Controls/ListBoxItem/ListBoxItem.jsx":"87638bfaa9be","components/Controls/MixerTable/MixerTable.jsx":"2f04e65e640c","components/Controls/NumberStepper/NumberStepper.jsx":"8b537c333dc8","components/Controls/SearchField/SearchField.jsx":"17c0f8be0fda","components/Controls/Segmented/Segmented.jsx":"5a36a982b5bb","components/Controls/Stepper/Stepper.jsx":"4afc3633bbbf","components/Controls/TextBox/TextBox.jsx":"24cb6f90813c","components/Controls/TileButton/TileButton.jsx":"d9a9e414c4ec","components/Data/DataBlock/DataBlock.jsx":"96927576866b","components/Data/Message/Message.jsx":"6be393ef4d70","components/Data/Modal/Modal.jsx":"bb26bf92ce7c","components/Data/Notice/Notice.jsx":"22deb25a8b4f","components/Data/StatusRow/StatusRow.jsx":"b229f10f1cf5","components/Foundations/Palette/Palette.jsx":"ee9a067aeeb8","components/Foundations/Surfaces/Surfaces.jsx":"ee47588a5a6d","components/Foundations/TypeScale/TypeScale.jsx":"e8d972f97043","components/Layout/GroupHead/GroupHead.jsx":"e77d14c57219","components/Layout/SettingsRow/SettingsRow.jsx":"3064fc77c274","components/Navigation/Tab/Tab.jsx":"3292a07d3291"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.D47DesignSystem_33498e = window.D47DesignSystem_33498e || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/Chrome/Card/Card.jsx
try { (() => {
// Card — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function Card({
  context,
  name,
  meta,
  selected,
  onClick,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: cx('d47-card', selected && 'selected'),
    onClick: onClick,
    style: style
  }, context && /*#__PURE__*/React.createElement("span", {
    className: "d47-card__context"
  }, context), /*#__PURE__*/React.createElement("span", {
    className: "d47-card__name"
  }, name), meta && /*#__PURE__*/React.createElement("span", {
    className: "d47-card__meta"
  }, meta));
}
const CardClasses = {
  "card": "d47-card",
  "selected": "selected"
};
Object.assign(__ds_scope, { Card, CardClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Chrome/Card/Card.jsx", error: String((e && e.message) || e) }); }

// components/Controls/ApiKey/ApiKey.jsx
try { (() => {
// ApiKey — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function ApiKey({
  last4 = '',
  stored = true,
  status,
  placeholder = 'Paste the new key',
  onSave,
  onVerify,
  style
}) {
  const [editing, setEditing] = React.useState(false);
  const [draft, setDraft] = React.useState('');
  if (editing) {
    return /*#__PURE__*/React.createElement("div", {
      className: "d47-apikey",
      style: style
    }, /*#__PURE__*/React.createElement("input", {
      className: "d47-apikey__input",
      autoFocus: true,
      placeholder: placeholder,
      value: draft,
      onChange: e => setDraft(e.target.value)
    }), /*#__PURE__*/React.createElement("button", {
      className: "d47-tile-button tall",
      onClick: () => {
        onSave && onSave(draft);
        setDraft('');
        setEditing(false);
      }
    }, "Save"), /*#__PURE__*/React.createElement("button", {
      className: "d47-tile-button tall",
      onClick: () => {
        setDraft('');
        setEditing(false);
      }
    }, "Cancel"));
  }
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-apikey",
    style: style
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-apikey__stored"
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-apikey__mask"
  }, stored ? '\u2022'.repeat(8) + last4 : '\u2014'), /*#__PURE__*/React.createElement("span", {
    className: cx('d47-apikey__status', !stored && 'missing')
  }, status || (stored ? 'Key stored' : 'No key'))), /*#__PURE__*/React.createElement("button", {
    className: "d47-tile-button tall",
    onClick: () => setEditing(true)
  }, "Replace"), /*#__PURE__*/React.createElement("button", {
    className: "d47-tile-button tall",
    disabled: !stored,
    onClick: onVerify
  }, "Verify"));
}
const ApiKeyClasses = {
  "apikey_stored": "d47-apikey__stored",
  "apikey_input": "d47-apikey__input"
};
Object.assign(__ds_scope, { ApiKey, ApiKeyClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/ApiKey/ApiKey.jsx", error: String((e && e.message) || e) }); }

// components/Controls/CheckboxTile/CheckboxTile.jsx
try { (() => {
// CheckboxTile — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function CheckboxTile({
  children,
  checked,
  onChange,
  caps,
  disabled,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    role: "checkbox",
    "aria-checked": !!checked,
    "aria-disabled": !!disabled,
    tabIndex: disabled ? -1 : 0,
    className: cx('d47-checkbox', checked && 'checked', caps && 'caps', disabled && 'disabled'),
    style: style,
    onClick: () => !disabled && onChange && onChange(!checked),
    onKeyDown: e => {
      if (!disabled && (e.key === ' ' || e.key === 'Enter')) {
        e.preventDefault();
        onChange && onChange(!checked);
      }
    }
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-checkbox__box"
  }), children);
}
const CheckboxTileClasses = {
  "checkbox": "d47-checkbox",
  "checked": "checked",
  "caps": "caps",
  "checkbox-grid": "d47-checkbox-grid",
  "cols-4": "cols-4",
  "check": "d47-check"
};
Object.assign(__ds_scope, { CheckboxTile, CheckboxTileClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/CheckboxTile/CheckboxTile.jsx", error: String((e && e.message) || e) }); }

// components/Controls/Dropdown/Dropdown.jsx
try { (() => {
// Dropdown — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function Dropdown({
  options = [],
  value,
  onChange,
  placeholder = '',
  inline,
  defaultOpen = false,
  style
}) {
  const [open, setOpen] = React.useState(defaultOpen);
  const opts = options.map(o => typeof o === 'string' ? {
    value: o,
    label: o
  } : o);
  const cur = opts.find(o => o.value === value);
  return /*#__PURE__*/React.createElement("div", {
    className: cx('d47-dropdown', inline && 'inline'),
    style: style
  }, /*#__PURE__*/React.createElement("button", {
    className: "d47-dropdown__button",
    onClick: () => setOpen(!open)
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-dropdown__value"
  }, cur ? cur.label : placeholder), /*#__PURE__*/React.createElement("span", {
    className: "d47-dropdown__caret"
  }, "\\u25BC")), open && /*#__PURE__*/React.createElement("div", {
    className: "d47-dropdown__list"
  }, opts.map(o => /*#__PURE__*/React.createElement("div", {
    key: o.value,
    className: cx('d47-dropdown__option', o.value === value && 'selected'),
    onClick: () => {
      onChange && onChange(o.value);
      setOpen(false);
    }
  }, o.label))));
}
const DropdownClasses = {
  "dropdown": "d47-dropdown",
  "inline": "inline"
};
Object.assign(__ds_scope, { Dropdown, DropdownClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/Dropdown/Dropdown.jsx", error: String((e && e.message) || e) }); }

// components/Controls/GlyphButton/GlyphButton.jsx
try { (() => {
// GlyphButton — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function GlyphButton({
  glyph = 'copy',
  label,
  onClick,
  disabled
}) {
  return /*#__PURE__*/React.createElement("span", {
    role: "button",
    tabIndex: disabled ? -1 : 0,
    "aria-label": label,
    className: cx('d47-glyph', disabled && 'disabled'),
    onClick: () => !disabled && onClick && onClick()
  }, label && !disabled && /*#__PURE__*/React.createElement("span", {
    className: "d47-glyph__tip"
  }, label), /*#__PURE__*/React.createElement("span", {
    className: "d47-glyph__face"
  }, glyph === 'copy' ? /*#__PURE__*/React.createElement("span", {
    className: "d47-copy-icon"
  }) : glyph));
}
const GlyphButtonClasses = {
  "glyph": "d47-glyph",
  "copy-icon": "d47-copy-icon",
  "disabled": "disabled"
};
Object.assign(__ds_scope, { GlyphButton, GlyphButtonClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/GlyphButton/GlyphButton.jsx", error: String((e && e.message) || e) }); }

// components/Controls/KeyBinding/KeyBinding.jsx
try { (() => {
// KeyBinding — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function KeyBinding({
  binding,
  onBind,
  onClear
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-keybind"
  }, /*#__PURE__*/React.createElement("span", {
    className: cx('d47-keybind__chip', !binding && 'unbound')
  }, binding || 'UNBOUND'), /*#__PURE__*/React.createElement("button", {
    className: "d47-tile-button tall",
    onClick: onBind
  }, "Bind"), /*#__PURE__*/React.createElement("button", {
    className: "d47-tile-button tall",
    onClick: onClear,
    disabled: !binding
  }, "Clear"));
}
const KeyBindingClasses = {
  "keybind": "d47-keybind",
  "keybind_chip": "d47-keybind__chip",
  "unbound": "unbound"
};
Object.assign(__ds_scope, { KeyBinding, KeyBindingClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/KeyBinding/KeyBinding.jsx", error: String((e && e.message) || e) }); }

// components/Controls/LevelBar/LevelBar.jsx
try { (() => {
// LevelBar — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function LevelBar({
  value = 0,
  onChange,
  muted,
  segments = 20,
  style
}) {
  const on = Math.round(value * segments);
  return /*#__PURE__*/React.createElement("div", {
    className: cx('d47-level', muted && 'muted'),
    style: style
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-level__track"
  }, Array.from({
    length: segments
  }, (_, i) => /*#__PURE__*/React.createElement("span", {
    key: i,
    className: cx('d47-level__seg', i < on && 'on'),
    onClick: () => onChange && onChange((i + 1) / segments)
  }))), /*#__PURE__*/React.createElement("span", {
    className: "d47-level__value"
  }, value.toFixed(2)));
}
const LevelBarClasses = {
  "level": "d47-level",
  "muted": "muted"
};
Object.assign(__ds_scope, { LevelBar, LevelBarClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/LevelBar/LevelBar.jsx", error: String((e && e.message) || e) }); }

// components/Controls/ListBoxItem/ListBoxItem.jsx
try { (() => {
// ListBoxItem — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function ListBoxItem({
  name,
  sub,
  aside,
  selected,
  disabled,
  compact,
  onClick
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: cx('d47-list-row', selected && 'selected', disabled && 'disabled', compact && 'compact'),
    onClick: () => !disabled && onClick && onClick()
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-list-row__main"
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-list-row__name"
  }, name), sub && /*#__PURE__*/React.createElement("span", {
    className: "d47-list-row__sub"
  }, sub)), aside && /*#__PURE__*/React.createElement("span", {
    className: "d47-list-row__aside"
  }, aside));
}
const ListBoxItemClasses = {
  "list": "d47-list",
  "list-head": "d47-list-head",
  "list-row": "d47-list-row"
};
Object.assign(__ds_scope, { ListBoxItem, ListBoxItemClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/ListBoxItem/ListBoxItem.jsx", error: String((e && e.message) || e) }); }

// components/Controls/MixerTable/MixerTable.jsx
try { (() => {
// MixerTable — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function MixerTable({
  channels = [],
  onChange,
  onReset,
  duckLabel = 'Duck while D47 speaks'
}) {
  const set = (id, patch) => onChange && onChange(id, patch);
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%'
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-mixer__head"
  }, /*#__PURE__*/React.createElement("div", null, "Channel"), /*#__PURE__*/React.createElement("div", null, "Level"), /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'center'
    }
  }, "Mute"), /*#__PURE__*/React.createElement("div", null, duckLabel), /*#__PURE__*/React.createElement("div", null)), channels.map(c => /*#__PURE__*/React.createElement("div", {
    key: c.id,
    className: "d47-mixer__row"
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-mixer__name"
  }, c.name), /*#__PURE__*/React.createElement(__ds_scope.LevelBar, {
    value: c.level,
    muted: c.muted,
    onChange: v => set(c.id, {
      level: v
    })
  }), /*#__PURE__*/React.createElement("div", {
    className: "d47-mixer__center"
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-mixer__mute",
    onClick: () => set(c.id, {
      muted: !c.muted
    })
  }, /*#__PURE__*/React.createElement("span", {
    className: cx('d47-check', c.muted && 'checked')
  }))), c.duck == null ? /*#__PURE__*/React.createElement("div", {
    className: "d47-mixer__none"
  }, "\\u2014") : /*#__PURE__*/React.createElement(__ds_scope.LevelBar, {
    value: c.duck,
    onChange: v => set(c.id, {
      duck: v
    })
  }), /*#__PURE__*/React.createElement("div", {
    className: "d47-row__reset"
  }, c.dirty && /*#__PURE__*/React.createElement(__ds_scope.GlyphButton, {
    glyph: "\\u21BA",
    label: "Reset to default",
    onClick: () => onReset && onReset(c.id)
  })))));
}
const MixerTableClasses = {
  "mixer_head": "d47-mixer__head",
  "mixer_row": "d47-mixer__row",
  "mixer_mute": "d47-mixer__mute",
  "mixer_none": "d47-mixer__none"
};
Object.assign(__ds_scope, { MixerTable, MixerTableClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/MixerTable/MixerTable.jsx", error: String((e && e.message) || e) }); }

// components/Controls/NumberStepper/NumberStepper.jsx
try { (() => {
// NumberStepper — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

function NumberStepper({
  value,
  onChange,
  step = 1,
  min = -Infinity,
  max = Infinity,
  format = v => String(v)
}) {
  const go = d => {
    const n = Math.min(max, Math.max(min, +(value + d * step).toFixed(6)));
    onChange && onChange(n);
  };
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-number-stepper"
  }, /*#__PURE__*/React.createElement("button", {
    className: "d47-stepper__arrow",
    "aria-label": "Decrease",
    disabled: value <= min,
    onClick: () => go(-1)
  }, "\\u25C4"), /*#__PURE__*/React.createElement("span", {
    className: "d47-stepper__value"
  }, format(value)), /*#__PURE__*/React.createElement("button", {
    className: "d47-stepper__arrow",
    "aria-label": "Increase",
    disabled: value >= max,
    onClick: () => go(1)
  }, "\\u25BA"));
}
const NumberStepperClasses = {
  "number-stepper": "d47-number-stepper"
};
Object.assign(__ds_scope, { NumberStepper, NumberStepperClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/NumberStepper/NumberStepper.jsx", error: String((e && e.message) || e) }); }

// components/Controls/SearchField/SearchField.jsx
try { (() => {
// SearchField — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

function SearchField({
  value,
  onChange,
  placeholder = 'Search this page'
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-search"
  }, /*#__PURE__*/React.createElement("input", {
    value: value,
    placeholder: placeholder,
    onChange: e => onChange && onChange(e.target.value)
  }));
}
const SearchFieldClasses = {
  "search": "d47-search"
};
Object.assign(__ds_scope, { SearchField, SearchFieldClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/SearchField/SearchField.jsx", error: String((e && e.message) || e) }); }

// components/Controls/Segmented/Segmented.jsx
try { (() => {
// Segmented — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function Segmented({
  options = [],
  value,
  onChange,
  cols,
  style
}) {
  return /*#__PURE__*/React.createElement("div", {
    role: "radiogroup",
    className: cx('d47-segmented', cols === 2 && 'cols-2', cols === 3 && 'cols-3'),
    style: style
  }, options.map(o => /*#__PURE__*/React.createElement("div", {
    key: o.value,
    role: "radio",
    "aria-checked": o.value === value,
    tabIndex: 0,
    className: cx('d47-segment', o.status && 'has-status', o.value === value && 'selected'),
    onClick: () => onChange && onChange(o.value)
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-segment__label"
  }, o.label), o.status && /*#__PURE__*/React.createElement("span", {
    className: cx('d47-segment__status', o.stored && 'stored')
  }, o.status))));
}
const SegmentedClasses = {
  "segmented": "d47-segmented",
  "cols-2": "cols-2",
  "cols-3": "cols-3",
  "segment": "d47-segment",
  "selected": "selected",
  "has-status": "has-status",
  "segment_label": "d47-segment__label",
  "stored": "stored"
};
Object.assign(__ds_scope, { Segmented, SegmentedClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/Segmented/Segmented.jsx", error: String((e && e.message) || e) }); }

// components/Controls/Stepper/Stepper.jsx
try { (() => {
// Stepper — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function Stepper({
  options = [],
  value,
  onChange,
  inherited,
  wrap = true,
  style
}) {
  const opts = options.map(o => typeof o === 'string' ? {
    value: o,
    label: o
  } : o);
  const i = Math.max(0, opts.findIndex(o => o.value === value));
  const n = opts.length;
  const go = d => {
    let j = i + d;
    if (wrap) j = (j + n) % n;else j = Math.min(n - 1, Math.max(0, j));
    onChange && onChange(opts[j].value);
  };
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-stepper",
    style: style
  }, /*#__PURE__*/React.createElement("button", {
    className: "d47-stepper__arrow",
    "aria-label": "Previous",
    onClick: () => go(-1)
  }, "\\u25C4"), /*#__PURE__*/React.createElement("div", {
    className: "d47-stepper__value"
  }, /*#__PURE__*/React.createElement("span", {
    className: cx('d47-stepper__text', inherited && 'inherited')
  }, opts[i] ? opts[i].label : ''), /*#__PURE__*/React.createElement("span", {
    className: "d47-stepper__count"
  }, n ? i + 1 + ' / ' + n : '')), /*#__PURE__*/React.createElement("button", {
    className: "d47-stepper__arrow",
    "aria-label": "Next",
    onClick: () => go(1)
  }, "\\u25BA"));
}
const StepperClasses = {
  "stepper": "d47-stepper"
};
Object.assign(__ds_scope, { Stepper, StepperClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/Stepper/Stepper.jsx", error: String((e && e.message) || e) }); }

// components/Controls/TextBox/TextBox.jsx
try { (() => {
// TextBox — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function TextBox({
  prefix,
  value,
  onChange,
  onSubmit,
  placeholder,
  disabled,
  error,
  warning,
  style
}) {
  const level = error ? 'invalid' : warning ? 'warning' : null;
  const msg = error || warning;
  const field = /*#__PURE__*/React.createElement("div", {
    className: cx('d47-field', disabled && 'disabled', level),
    style: msg ? undefined : style
  }, prefix && /*#__PURE__*/React.createElement("span", {
    className: "d47-field__prefix"
  }, prefix), /*#__PURE__*/React.createElement("input", {
    value: value,
    placeholder: placeholder,
    disabled: disabled,
    "aria-invalid": !!error,
    onChange: e => onChange && onChange(e.target.value),
    onKeyDown: e => {
      if (e.key === 'Enter' && onSubmit) onSubmit(e.target.value);
    }
  }));
  if (!msg || typeof msg !== 'string') return field;
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-field-wrap",
    style: style
  }, field, /*#__PURE__*/React.createElement("span", {
    className: cx('d47-field-message', !error && 'warning'),
    role: error ? 'alert' : undefined
  }, msg));
}
const TextBoxClasses = {
  "field": "d47-field",
  "invalid": "invalid",
  "warning": "warning",
  "field-wrap": "d47-field-wrap",
  "field-message": "d47-field-message"
};
Object.assign(__ds_scope, { TextBox, TextBoxClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/TextBox/TextBox.jsx", error: String((e && e.message) || e) }); }

// components/Controls/TileButton/TileButton.jsx
try { (() => {
// TileButton — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function TileButton({
  children,
  variant = 'default',
  size = 'default',
  selected,
  disabled,
  onClick,
  style
}) {
  return /*#__PURE__*/React.createElement("button", {
    className: cx('d47-tile-button', variant === 'destructive' && 'destructive', size !== 'default' && size, selected && 'selected'),
    disabled: disabled,
    onClick: onClick,
    style: style
  }, children);
}
const TileButtonClasses = {
  "tile-button": "d47-tile-button",
  "destructive": "destructive",
  "tall": "tall",
  "full": "full",
  "compact": "compact",
  "selected": "selected",
  "is-pressed": "is-pressed"
};
Object.assign(__ds_scope, { TileButton, TileButtonClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Controls/TileButton/TileButton.jsx", error: String((e && e.message) || e) }); }

// components/Data/DataBlock/DataBlock.jsx
try { (() => {
// DataBlock — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function DataBlock({
  label,
  value,
  tone = 'orange',
  small,
  span2,
  prose,
  gauge,
  gaugeKind = 'progress'
}) {
  if (prose != null) return /*#__PURE__*/React.createElement("div", {
    className: cx('d47-data prose', span2 && 'span-2')
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-data__label"
  }, label), prose);
  const v = /*#__PURE__*/React.createElement("span", {
    className: cx('d47-data__value', tone !== 'orange' && tone, small && 'small', gauge != null && 'd47-data__end')
  }, value);
  if (gauge != null) {
    return /*#__PURE__*/React.createElement("div", {
      className: cx('d47-data', span2 && 'span-2'),
      style: {
        gap: 5
      }
    }, /*#__PURE__*/React.createElement("div", {
      className: "d47-data__row"
    }, /*#__PURE__*/React.createElement("span", {
      className: "d47-data__label"
    }, label), v), /*#__PURE__*/React.createElement("div", {
      className: cx('d47-gauge', gaugeKind !== 'progress' && gaugeKind)
    }, /*#__PURE__*/React.createElement("div", {
      className: "d47-gauge__fill",
      style: {
        width: Math.min(100, gauge * 100) + '%'
      }
    })));
  }
  return /*#__PURE__*/React.createElement("div", {
    className: cx('d47-data', span2 && 'span-2')
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-data__label"
  }, label), v);
}
const DataBlockClasses = {
  "data-grid": "d47-data-grid",
  "span-2": "span-2",
  "data": "d47-data",
  "gauge": "d47-gauge",
  "capacity": "capacity",
  "over": "over",
  "ladder_state": "d47-ladder__state",
  "met": "met",
  "unknown": "unknown",
  "unmet": "unmet"
};
Object.assign(__ds_scope, { DataBlock, DataBlockClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Data/DataBlock/DataBlock.jsx", error: String((e && e.message) || e) }); }

// components/Data/Message/Message.jsx
try { (() => {
// Message — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function Message({
  from = 'd47',
  name,
  intent,
  delivery,
  time,
  children,
  chips,
  cost
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: cx('d47-message', from === 'cmdr' && 'cmdr')
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-message__head"
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-message__name"
  }, name || (from === 'cmdr' ? 'CMDR' : 'D47')), intent && /*#__PURE__*/React.createElement("span", {
    className: "d47-message__intent"
  }, intent), delivery && /*#__PURE__*/React.createElement("span", {
    className: "d47-message__delivery"
  }, delivery), time && /*#__PURE__*/React.createElement("span", {
    className: "d47-message__time"
  }, time)), /*#__PURE__*/React.createElement("div", {
    className: "d47-message__body"
  }, children), chips && chips.length > 0 && /*#__PURE__*/React.createElement("div", {
    className: "d47-message__chips"
  }, chips.map((c, i) => /*#__PURE__*/React.createElement("span", {
    key: i,
    className: cx('d47-message__chip', c.here && 'here')
  }, c.label))), cost && cost.length > 0 && /*#__PURE__*/React.createElement("div", {
    className: "d47-message__cost"
  }, cost.map((p, i) => /*#__PURE__*/React.createElement(React.Fragment, {
    key: i
  }, i > 0 && /*#__PURE__*/React.createElement("span", null, "\\u00B7"), /*#__PURE__*/React.createElement("span", {
    className: i === cost.length - 1 ? 'd47-message__cost-value' : undefined
  }, p)))));
}
const MessageClasses = {
  "messages": "d47-messages",
  "message": "d47-message",
  "cmdr": "cmdr"
};
Object.assign(__ds_scope, { Message, MessageClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Data/Message/Message.jsx", error: String((e && e.message) || e) }); }

// components/Data/Modal/Modal.jsx
try { (() => {
// Modal — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

function Modal({
  open = true,
  dek,
  title,
  figureLabel,
  figureValue,
  children,
  footer,
  onClose,
  contained
}) {
  if (!open) return null;
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-scrim",
    style: contained ? undefined : {
      position: 'fixed'
    },
    onClick: e => {
      if (e.target === e.currentTarget && onClose) onClose();
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-modal",
    role: "dialog",
    "aria-label": title
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-modal__head"
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-title-block__text"
  }, dek && /*#__PURE__*/React.createElement("span", {
    className: "d47-modal__dek"
  }, dek), /*#__PURE__*/React.createElement("span", {
    className: "d47-modal__title"
  }, title)), figureValue && /*#__PURE__*/React.createElement("div", {
    className: "d47-title-block__figure"
  }, figureLabel && /*#__PURE__*/React.createElement("span", {
    className: "d47-title-block__figure-label"
  }, figureLabel), /*#__PURE__*/React.createElement("span", {
    className: "d47-modal__figure-value"
  }, figureValue))), /*#__PURE__*/React.createElement("div", {
    className: "d47-modal__body"
  }, children), /*#__PURE__*/React.createElement("div", {
    className: "d47-modal__foot"
  }, footer || /*#__PURE__*/React.createElement("button", {
    className: "d47-tile-button tall",
    onClick: onClose
  }, "Close"))));
}
const ModalClasses = {
  "scrim": "d47-scrim",
  "modal": "d47-modal",
  "breakdown": "d47-breakdown"
};
Object.assign(__ds_scope, { Modal, ModalClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Data/Modal/Modal.jsx", error: String((e && e.message) || e) }); }

// components/Data/Notice/Notice.jsx
try { (() => {
// Notice — an error or warning message. Renders the d47 kit markup (components/kit.css).

const cx = (...a) => a.filter(Boolean).join(' ');
function Notice({
  level = 'error',
  label,
  children,
  detail,
  actions,
  inline,
  style
}) {
  const warn = level === 'warning';
  return /*#__PURE__*/React.createElement("div", {
    className: cx('d47-notice', warn && 'warning', inline && 'inline'),
    role: warn ? 'status' : 'alert',
    style: style
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-notice__main"
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-notice__label"
  }, label || (warn ? 'Warning' : 'Error')), children && /*#__PURE__*/React.createElement("span", {
    className: "d47-notice__text"
  }, children), detail && /*#__PURE__*/React.createElement("span", {
    className: "d47-notice__detail"
  }, detail)), actions && /*#__PURE__*/React.createElement("div", {
    className: "d47-notice__actions"
  }, actions));
}
const NoticeClasses = {
  "notice": "d47-notice",
  "warning": "warning",
  "inline": "inline"
};
Object.assign(__ds_scope, { Notice, NoticeClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Data/Notice/Notice.jsx", error: String((e && e.message) || e) }); }

// components/Data/StatusRow/StatusRow.jsx
try { (() => {
// StatusRow — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

function StatusRow({
  status = 'PTT ready',
  cost,
  onSpend
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-status"
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-status__dot"
  }), /*#__PURE__*/React.createElement("span", {
    className: "d47-status__text"
  }, status), cost && /*#__PURE__*/React.createElement("span", {
    className: "d47-status__session"
  }, "Session ", /*#__PURE__*/React.createElement("span", {
    className: "d47-status__cost"
  }, cost)), /*#__PURE__*/React.createElement("button", {
    className: "d47-tile-button compact",
    onClick: onSpend
  }, "Spend"));
}
const StatusRowClasses = {
  "status": "d47-status",
  "composer": "d47-composer"
};
Object.assign(__ds_scope, { StatusRow, StatusRowClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Data/StatusRow/StatusRow.jsx", error: String((e && e.message) || e) }); }

// components/Foundations/Palette/Palette.jsx
try { (() => {
// Palette — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const ROLES = {
  bg: 'Window ground.',
  bar: 'Title bar and modal ground.',
  slab: 'Read-only data and disabled ground.',
  white: 'Names, titles and speech.',
  grey: 'Labels, helper prose, unavailable.',
  grey2: 'Placeholders, disabled text.',
  a: 'Accent: can act, and values.',
  knock: 'Text on a solid accent fill.',
  brown: 'Labels inside a selected row.',
  cyan: 'Yours, current or ready.',
  blue: 'Confirmed or met.',
  yellow: 'Stored or equipped.',
  red: 'Destructive, hostile, locked; errors.',
  warn: 'Warnings: caution.',
  tile: 'Accent 20% into bg. At rest.',
  tile2: 'Accent 30% into bg. Hover.',
  line: 'Accent 55% into bg. Frame.',
  line2: 'Accent 28% into bg. Separators.'
};
function Palette({
  tokens = Object.keys(ROLES)
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "swatches",
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
      gap: '6px 22px'
    }
  }, tokens.map(t => /*#__PURE__*/React.createElement("div", {
    key: t,
    style: {
      display: 'grid',
      gridTemplateColumns: '30px 60px minmax(0, 1fr)',
      alignItems: 'center',
      gap: 10,
      fontSize: 12
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: 30,
      height: 18,
      background: 'var(--d47-' + t + ')',
      border: '1px solid rgba(128,128,128,0.35)'
    }
  }), /*#__PURE__*/React.createElement("span", {
    className: "d47-mono",
    style: {
      color: 'var(--d47-white)'
    }
  }, t), /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--d47-grey)'
    }
  }, ROLES[t] || ''))));
}
const PaletteClasses = {};
Object.assign(__ds_scope, { Palette, PaletteClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Foundations/Palette/Palette.jsx", error: String((e && e.message) || e) }); }

// components/Foundations/Surfaces/Surfaces.jsx
try { (() => {
// Surfaces — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

function Surfaces({
  brand = 'DIRECTIVE 47',
  version,
  children,
  scanlines = true,
  showControls = true,
  style,
  bodyStyle
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-window",
    style: style
  }, /*#__PURE__*/React.createElement("div", {
    className: "d47-titlebar"
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-brand-mark"
  }), /*#__PURE__*/React.createElement("span", {
    className: "d47-brand-name"
  }, brand), version && /*#__PURE__*/React.createElement("span", {
    className: "d47-version"
  }, version), showControls && /*#__PURE__*/React.createElement("div", {
    className: "d47-window-controls"
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-window-control"
  }, "\\u2014"), /*#__PURE__*/React.createElement("span", {
    className: "d47-window-control"
  }, "\\u25A1"), /*#__PURE__*/React.createElement("span", {
    className: "d47-window-control close"
  }, "\\u2715"))), /*#__PURE__*/React.createElement("div", {
    className: "d47-window__body",
    style: bodyStyle
  }, children), scanlines && /*#__PURE__*/React.createElement("div", {
    className: "d47-scanlines"
  }));
}
const SurfacesClasses = {
  "window": "d47-window",
  "titlebar": "d47-titlebar",
  "window_body": "d47-window__body",
  "title-block": "d47-title-block",
  "section-head": "d47-section-head",
  "scanlines": "d47-scanlines"
};
Object.assign(__ds_scope, { Surfaces, SurfacesClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Foundations/Surfaces/Surfaces.jsx", error: String((e && e.message) || e) }); }

// components/Foundations/TypeScale/TypeScale.jsx
try { (() => {
// TypeScale — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

function TypeScale({
  samples = {}
}) {
  const s = Object.assign({
    title: 'Sacred Fire',
    tab: 'Transcript',
    group: 'Microphone',
    label: "How D47 knows you're talking to it",
    body: 'Functioning within tolerance, Commander.',
    control: 'Add to checklist',
    meta: '19:42 \u00B7 $0.1075'
  }, samples);
  const cap = {
    width: 110,
    fontFamily: 'var(--d47-font-mono)',
    fontSize: 11,
    color: 'var(--d47-grey2)'
  };
  const row = {
    display: 'flex',
    alignItems: 'baseline',
    gap: 14
  };
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 12
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: row
  }, /*#__PURE__*/React.createElement("span", {
    style: cap
  }, "title 28"), /*#__PURE__*/React.createElement("span", {
    className: "d47-title"
  }, s.title)), /*#__PURE__*/React.createElement("div", {
    style: row
  }, /*#__PURE__*/React.createElement("span", {
    style: cap
  }, "tab 15"), /*#__PURE__*/React.createElement("span", {
    className: "d47-tab active"
  }, s.tab)), /*#__PURE__*/React.createElement("div", {
    style: row
  }, /*#__PURE__*/React.createElement("span", {
    style: cap
  }, "group 15/600"), /*#__PURE__*/React.createElement("span", {
    className: "d47-group-head__name"
  }, s.group)), /*#__PURE__*/React.createElement("div", {
    style: row
  }, /*#__PURE__*/React.createElement("span", {
    style: cap
  }, "row label 15"), /*#__PURE__*/React.createElement("span", {
    className: "d47-row__label"
  }, s.label)), /*#__PURE__*/React.createElement("div", {
    style: row
  }, /*#__PURE__*/React.createElement("span", {
    style: cap
  }, "body 16"), /*#__PURE__*/React.createElement("span", {
    className: "d47-message__body"
  }, s.body)), /*#__PURE__*/React.createElement("div", {
    style: row
  }, /*#__PURE__*/React.createElement("span", {
    style: cap
  }, "control 13\\u201314"), /*#__PURE__*/React.createElement("span", {
    className: "d47-chrome",
    style: {
      fontSize: 13,
      fontWeight: 600
    }
  }, s.control)), /*#__PURE__*/React.createElement("div", {
    style: row
  }, /*#__PURE__*/React.createElement("span", {
    style: cap
  }, "meta 11\\u201312"), /*#__PURE__*/React.createElement("span", {
    className: "d47-mono",
    style: {
      fontSize: 12,
      color: 'var(--d47-grey2)'
    }
  }, s.meta)));
}
const TypeScaleClasses = {
  "title": "d47-title",
  "chrome": "d47-chrome",
  "mono": "d47-mono"
};
Object.assign(__ds_scope, { TypeScale, TypeScaleClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Foundations/TypeScale/TypeScale.jsx", error: String((e && e.message) || e) }); }

// components/Layout/GroupHead/GroupHead.jsx
try { (() => {
// GroupHead — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

function GroupHead({
  name,
  description,
  dirty,
  onReset
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: "d47-group-head"
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-group-head__name"
  }, name), description && /*#__PURE__*/React.createElement("span", {
    className: "d47-group-head__desc"
  }, description), /*#__PURE__*/React.createElement("span", {
    className: "d47-group-head__reset"
  }, dirty && /*#__PURE__*/React.createElement(__ds_scope.GlyphButton, {
    glyph: "\\u21BA",
    label: "Reset to default",
    onClick: onReset
  })));
}
const GroupHeadClasses = {
  "page-head": "d47-page-head",
  "group-head": "d47-group-head"
};
Object.assign(__ds_scope, { GroupHead, GroupHeadClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Layout/GroupHead/GroupHead.jsx", error: String((e && e.message) || e) }); }

// components/Layout/SettingsRow/SettingsRow.jsx
try { (() => {
// SettingsRow — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function SettingsRow({
  label,
  children,
  protected: prot,
  dirty,
  onReset
}) {
  return /*#__PURE__*/React.createElement("div", {
    className: cx('d47-row', prot && 'protected')
  }, /*#__PURE__*/React.createElement("span", {
    className: "d47-row__label"
  }, label), /*#__PURE__*/React.createElement("div", {
    className: "d47-row__control"
  }, children), /*#__PURE__*/React.createElement("span", {
    className: "d47-row__reset"
  }, dirty && /*#__PURE__*/React.createElement(__ds_scope.GlyphButton, {
    glyph: "\\u21BA",
    label: "Reset to default",
    onClick: onReset
  })));
}
const SettingsRowClasses = {
  "settings": "d47-settings",
  "row": "d47-row",
  "protected": "protected",
  "inset": "d47-inset"
};
Object.assign(__ds_scope, { SettingsRow, SettingsRowClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Layout/SettingsRow/SettingsRow.jsx", error: String((e && e.message) || e) }); }

// components/Navigation/Tab/Tab.jsx
try { (() => {
// Tab — renders the d47 kit markup (components/kit.css). Colour and type come from tokens.

const cx = (...a) => a.filter(Boolean).join(' ');
function Tab({
  tabs = [],
  value,
  onChange,
  variant = 'tabs'
}) {
  const opts = tabs.map(t => typeof t === 'string' ? {
    value: t,
    label: t
  } : t);
  const sub = variant === 'subtabs';
  const strip = /*#__PURE__*/React.createElement("div", {
    role: "tablist",
    className: sub ? 'd47-subtabs' : 'd47-tabs'
  }, opts.map(o => /*#__PURE__*/React.createElement("span", {
    key: o.value,
    role: "tab",
    tabIndex: 0,
    "aria-selected": o.value === value,
    className: cx(sub ? 'd47-subtab' : 'd47-tab', o.value === value && 'active'),
    onClick: () => onChange && onChange(o.value)
  }, o.label)));
  return sub ? strip : /*#__PURE__*/React.createElement("div", null, strip, /*#__PURE__*/React.createElement("div", {
    className: "d47-tabs-rule"
  }));
}
const TabClasses = {
  "tabs": "d47-tabs",
  "tab": "d47-tab",
  "active": "active",
  "subtabs": "d47-subtabs",
  "subtab": "d47-subtab",
  "subbar": "d47-subbar",
  "sidebar": "d47-sidebar"
};
Object.assign(__ds_scope, { Tab, TabClasses });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/Navigation/Tab/Tab.jsx", error: String((e && e.message) || e) }); }

__ds_ns.Card = __ds_scope.Card;

__ds_ns.CardClasses = __ds_scope.CardClasses;

__ds_ns.ApiKey = __ds_scope.ApiKey;

__ds_ns.ApiKeyClasses = __ds_scope.ApiKeyClasses;

__ds_ns.CheckboxTile = __ds_scope.CheckboxTile;

__ds_ns.CheckboxTileClasses = __ds_scope.CheckboxTileClasses;

__ds_ns.Dropdown = __ds_scope.Dropdown;

__ds_ns.DropdownClasses = __ds_scope.DropdownClasses;

__ds_ns.GlyphButton = __ds_scope.GlyphButton;

__ds_ns.GlyphButtonClasses = __ds_scope.GlyphButtonClasses;

__ds_ns.KeyBinding = __ds_scope.KeyBinding;

__ds_ns.KeyBindingClasses = __ds_scope.KeyBindingClasses;

__ds_ns.LevelBar = __ds_scope.LevelBar;

__ds_ns.LevelBarClasses = __ds_scope.LevelBarClasses;

__ds_ns.ListBoxItem = __ds_scope.ListBoxItem;

__ds_ns.ListBoxItemClasses = __ds_scope.ListBoxItemClasses;

__ds_ns.MixerTable = __ds_scope.MixerTable;

__ds_ns.MixerTableClasses = __ds_scope.MixerTableClasses;

__ds_ns.NumberStepper = __ds_scope.NumberStepper;

__ds_ns.NumberStepperClasses = __ds_scope.NumberStepperClasses;

__ds_ns.SearchField = __ds_scope.SearchField;

__ds_ns.SearchFieldClasses = __ds_scope.SearchFieldClasses;

__ds_ns.Segmented = __ds_scope.Segmented;

__ds_ns.SegmentedClasses = __ds_scope.SegmentedClasses;

__ds_ns.Stepper = __ds_scope.Stepper;

__ds_ns.StepperClasses = __ds_scope.StepperClasses;

__ds_ns.TextBox = __ds_scope.TextBox;

__ds_ns.TextBoxClasses = __ds_scope.TextBoxClasses;

__ds_ns.TileButton = __ds_scope.TileButton;

__ds_ns.TileButtonClasses = __ds_scope.TileButtonClasses;

__ds_ns.DataBlock = __ds_scope.DataBlock;

__ds_ns.DataBlockClasses = __ds_scope.DataBlockClasses;

__ds_ns.Message = __ds_scope.Message;

__ds_ns.MessageClasses = __ds_scope.MessageClasses;

__ds_ns.Modal = __ds_scope.Modal;

__ds_ns.ModalClasses = __ds_scope.ModalClasses;

__ds_ns.Notice = __ds_scope.Notice;

__ds_ns.NoticeClasses = __ds_scope.NoticeClasses;

__ds_ns.StatusRow = __ds_scope.StatusRow;

__ds_ns.StatusRowClasses = __ds_scope.StatusRowClasses;

__ds_ns.Palette = __ds_scope.Palette;

__ds_ns.PaletteClasses = __ds_scope.PaletteClasses;

__ds_ns.Surfaces = __ds_scope.Surfaces;

__ds_ns.SurfacesClasses = __ds_scope.SurfacesClasses;

__ds_ns.TypeScale = __ds_scope.TypeScale;

__ds_ns.TypeScaleClasses = __ds_scope.TypeScaleClasses;

__ds_ns.GroupHead = __ds_scope.GroupHead;

__ds_ns.GroupHeadClasses = __ds_scope.GroupHeadClasses;

__ds_ns.SettingsRow = __ds_scope.SettingsRow;

__ds_ns.SettingsRowClasses = __ds_scope.SettingsRowClasses;

__ds_ns.Tab = __ds_scope.Tab;

__ds_ns.TabClasses = __ds_scope.TabClasses;

})();
