/**
 * An array of extension manifests for the Articulate dashboard.
 * @type {Array<UmbExtensionManifest>}
 */
export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'dashboard',
    alias: 'Articulate.BackOffice.Dashboard',
    name: 'Articulate BackOffice Dashboard',
    js: async () => await import('./dashboard.element.js'),
    weight: 10,
    meta: {
      label: 'Articulate Dashboard',
      pathname: 'articulate-dashboard',
    },
    conditions: [
      {
        alias: 'Umb.Condition.SectionAlias',
        match: 'Umb.Section.Settings',
      },
    ],
  },
];
