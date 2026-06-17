import type { ManifestApi } from '@umbraco-cms/backoffice/extension-api';

export interface ManifestArticulateMarkdownEditorAction extends ManifestApi<any> {
    type: 'articulateMarkdownEditorAction';
    meta?: MetaArticulateMarkdownEditorAction;
}

export type MetaArticulateMarkdownEditorAction = {
    icon?: string | null;
    label?: string | null;
};

declare global {
    interface ArticulateExtensionManifestMap {
        articulateMarkdownEditorAction: ManifestArticulateMarkdownEditorAction;
    }
}
