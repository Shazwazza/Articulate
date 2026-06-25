import { manifests as dashboards } from './dashboards/manifests.js';
import { manifests as editors } from './editors/manifests.js';
import { manifests as entrypoints } from './entrypoints/manifests.js';
import { manifests as propertyValuePresets } from './property-value-presets/manifests.js';
import { manifests as markdownEditor } from './packages/articulate-markdown-editor/property-editors/manifests.js';

// Job of the bundle is to collate all the manifests from different parts of the extension and load other manifests
// We load this bundle from umbraco-package.json
export const manifests: Array<UmbExtensionManifest> = [
  ...entrypoints,
  ...dashboards,
  ...editors,
  ...propertyValuePresets,
  ...markdownEditor,
];
