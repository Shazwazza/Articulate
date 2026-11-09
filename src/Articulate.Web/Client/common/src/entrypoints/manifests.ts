/**
 * An array of extension manifests for the Articulate backoffice entry point.
 * @type {Array<UmbExtensionManifest>}
 */
export const manifests: Array<UmbExtensionManifest> = [
  {
    name: 'Articulate Entrypoint',
    alias: 'Articulate.Entrypoint',
    type: 'backofficeEntryPoint',
    js: async () => await import('./entrypoint.js'),
  },
];
