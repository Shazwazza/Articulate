import type { ManifestPropertyValuePreset } from '@umbraco-cms/backoffice/property';

const articulateStringPreset: ManifestPropertyValuePreset = {
  type: 'propertyValuePreset',
  alias: 'Articulate.PropertyValuePreset.BlogDefaults.String',
  name: 'Articulate Blog Defaults Preset for String Properties',
  weight: 100,
  forPropertyEditorUiAlias: 'Umb.PropertyEditorUi.TextBox',
  api: () => import('./articulate.property-value-preset.js'),
};

const articulateIntegerPreset: ManifestPropertyValuePreset = {
  type: 'propertyValuePreset',
  alias: 'Articulate.PropertyValuePreset.BlogDefaults.Integer',
  name: 'Articulate Blog Defaults Preset for Integer Properties',
  weight: 100,
  forPropertyEditorSchemaAlias: 'Umbraco.Integer',
  api: () => import('./articulate.property-value-preset.js'),
};

const articulateThemePreset: ManifestPropertyValuePreset = {
  type: 'propertyValuePreset',
  alias: 'Articulate.PropertyValuePreset.BlogDefaults.Theme',
  name: 'Articulate Blog Defaults Preset for Theme Picker',
  weight: 100,
  forPropertyEditorUiAlias: 'ArticulateThemePicker',
  api: () => import('./articulate.property-value-preset.js'),
};

const articulateTogglePreset: ManifestPropertyValuePreset = {
  type: 'propertyValuePreset',
  alias: 'Articulate.PropertyValuePreset.BlogDefaults.Toggle',
  name: 'Articulate Blog Defaults Preset for Toggle Properties',
  weight: 100,
  forPropertyEditorSchemaAlias: 'Umbraco.TrueFalse',
  api: () => import('./articulate.property-value-preset.js'),
};

export const manifests: Array<ManifestPropertyValuePreset> = [
  articulateStringPreset,
  articulateIntegerPreset,
  articulateThemePreset,
  articulateTogglePreset,
];
