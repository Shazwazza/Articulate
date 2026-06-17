
export const manifests: Array<UmbExtensionManifest> = [
    {
        type: 'propertyEditorUi',
        // Maps to [DataEditor(ArticulateMarkdownEditor
        alias: 'Articulate.MarkdownEditor',
        name: 'Articulate Markdown Editor Property Editor UI',
        element: () => import('./property-editor-ui-markdown-editor.element.js'),
        meta: {
            label: 'Articulate Markdown Editor',
            propertyEditorSchemaAlias: 'Umbraco.MarkdownEditor',
            icon: 'icon-code',
            group: 'richContent',
            supportsReadOnly: true,
            settings: {
                properties: [
                    {
                        alias: 'preview',
                        label: 'Preview',
                        description: 'Display a live preview',
                        propertyEditorUiAlias: 'Umb.PropertyEditorUi.Toggle',
                    },
                    {
                        alias: 'defaultValue',
                        label: 'Default value',
                        description: 'If value is blank, the editor will show this',
                        propertyEditorUiAlias: 'Articulate.MarkdownEditor',
                    },
                    {
                        alias: 'overlaySize',
                        label: 'Overlay Size',
                        description: 'Select the width of the overlay.',
                        propertyEditorUiAlias: 'Umb.PropertyEditorUi.OverlaySize',
                    },
                ],
            },
        },
    },
];
