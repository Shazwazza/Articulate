/**
 * An array of extension manifests for the Articulate editors.
 * This is used by Umbraco to register the custom property editors.
 * @type {Array<UmbExtensionManifest>}
 */
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'propertyEditorUi',
    // matches to [DataEditor("ArticulateThemePicker")]
    alias: 'ArticulateThemePicker',
    name: 'Articulate Theme Picker',
    elementName: 'theme-picker-element',
    js: async () => await import('./theme-picker.element.js'),
    meta: {
      label: 'Articulate Theme Picker',
      propertyEditorSchemaAlias: 'Umbraco.Plain.String',
      icon: 'icon-palette',
      group: 'pickers',
    },
  },
];
