export const name = 'Articulate.MarkdownEditor';

// Full clone of Umbraco.Web.UI.Client/src/packages/markdown-editor
// Prevent any conflicts with the markdown editor in the backoffice
// TODO: Remove this when HeyRed replaced with Markdig package in Umbraco 18
export const extensions = [
    {
        name: 'Articulate Markdown Editor Bundle',
        alias: 'Articulate.Bundle.MarkdownEditor',
        type: 'bundle',
        js: () => import('./manifests.js'),
    },
];
