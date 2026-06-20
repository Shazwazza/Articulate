#!/usr/bin/env node
// tools/check-shared.mjs — verifies the 28 byte-identical source files between v17 and v18.
// Exits 0 if all match, 1 if any drift. Wired into `pnpm check` in both v17 and v18.
// Run from either lane: `pnpm check:shared` (or `node ../tools/check-shared.mjs` from v17/v18).

import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(here, '..');

// Paths relative to v17/src/ or v18/src/.
// Includes files that have relative `../api/*` imports — they happen to be byte-identical
// today but are technically independent. Drift = user investigates intent.
const SHARED = [
    'components/blogml-exporter.element.ts',
    'components/dashboard-options.element.ts',
    'components/theme-options.element.ts',
    'dashboards/dashboard.element.ts',
    'dashboards/manifests.ts',
    'editors/manifests.ts',
    'entrypoints/entrypoint.ts',
    'entrypoints/manifests.ts',
    'localization/en.ts',
    'main.ts',
    'packages/articulate-markdown-editor/index.ts',
    'packages/articulate-markdown-editor/manifests.ts',
    'packages/articulate-markdown-editor/monaco-markdown-editor-action.extension.ts',
    'packages/articulate-markdown-editor/components/index.ts',
    'packages/articulate-markdown-editor/components/input-markdown-editor/index.ts',
    'packages/articulate-markdown-editor/property-editors/manifests.ts',
    'packages/articulate-markdown-editor/property-editors/markdown-editor/manifests.ts',
    // Keep both lanes byte-identical so the validation wrapper retains its
    // UmbFormControlMixin / mandatory / addFormControlElement plumbing.
    'packages/articulate-markdown-editor/property-editors/markdown-editor/property-editor-ui-markdown-editor.element.ts',
    'packages/articulate-markdown-editor/property-editors/markdown-editor/types.ts',
    'property-value-presets/articulate.property-value-preset.ts',
    'property-value-presets/manifests.ts',
    'utils/document-node-utils.ts',
    'utils/error-utils.ts',
    'utils/form-utils.ts',
    'utils/notification-utils.ts',
    'utils/style-utils.ts',
    'utils/template-utils.ts',
    'vite-env.d.ts',
];

// Known INTENTIONAL lane divergences — NOT in SHARED on purpose:
//   components/blogml-importer.element.ts        — v18 adds numeric-response normalization (v18 API shape)
//   editors/theme-picker.element.ts              — v18 uses UmbChangeEvent (replaces UmbPropertyValueChangeEvent)
//   packages/.../input-markdown-editor/input-markdown.element.ts — diverges by a single blank line (cosmetic drift)
//   packages/.../articulate-markdown-editor/umbraco-package.ts   — dead code in both lanes; v18 uses { manifests } import shape

let mismatches = 0;
let missing = 0;
for (const rel of SHARED) {
    const v17 = resolve(clientRoot, 'v17', 'src', rel);
    const v18 = resolve(clientRoot, 'v18', 'src', rel);
    let a, b;
    try { a = readFileSync(v17, 'utf8'); } catch { console.error(`MISSING v17: ${rel}`); missing++; mismatches++; continue; }
    try { b = readFileSync(v18, 'utf8'); } catch { console.error(`MISSING v18: ${rel}`); missing++; mismatches++; continue; }
    if (a === b) {
        console.log(`OK    ${rel}`);
    } else {
        console.error(`DIFF  ${rel}`);
        mismatches++;
    }
}

const matched = SHARED.length - mismatches;
console.log(`\n${matched}/${SHARED.length} shared files match${missing > 0 ? ` (${missing} missing)` : ''}.`);
process.exit(mismatches > 0 ? 1 : 0);
