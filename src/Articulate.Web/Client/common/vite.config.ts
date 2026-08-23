import type { Plugin } from 'vite';
import path from 'node:path';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { copyFile, mkdir, writeFile, unlink, rm } from 'node:fs/promises';
import { createRequire } from 'node:module';

// --- CONSTANTS & PATHS ---
// v17 lives under Client/, so WEB_ROOT is two levels up from this workspace folder.
// Vite runs from the selected lane package. Keep the implementation here and
// let each lane provide only its package metadata/API/config shim.
const UI_ROOT = process.cwd();
const require = createRequire(path.resolve(UI_ROOT, 'package.json'));
const { defineConfig } = require('vite');
const { build: esbuildBuild } = require('esbuild');
const lightningcss = require('lightningcss');
const UI_ENTRY = path.resolve(UI_ROOT, '../common/src/main.ts');
const WEB_ROOT = path.resolve(UI_ROOT, '../..');
const UI_OUT = path.resolve(WEB_ROOT, 'wwwroot/App_Plugins/Articulate/BackOffice');
const PACKAGE_ROOT = path.resolve(WEB_ROOT, 'wwwroot/App_Plugins/Articulate');
const PUBLIC_ROOT = path.resolve(UI_ROOT, '../common/public');

// Static asset locations
const WEB_THEMES = path.resolve(WEB_ROOT, 'wwwroot/App_Plugins/Articulate/Themes');
const WEB_MARKDOWN = path.resolve(WEB_ROOT, 'wwwroot/App_Plugins/Articulate/MarkdownEditor');
const BUILD_VERSION_ENV_VAR = 'ARTICULATE_APP_VERSION';
const PACKAGE_JSON_PATH = path.resolve(UI_ROOT, 'package.json');

const defaultBuildVersion = (() => {
  try {
    const packageJson = JSON.parse(readFileSync(PACKAGE_JSON_PATH, 'utf8')) as { version?: string };
    return packageJson.version?.trim() || '0.0.0-dev';
  } catch {
    return '0.0.0-dev';
  }
})();

const buildVersion = {
  value: defaultBuildVersion,
};

// The active lane is derived from the lane package directory (`process.cwd()`).
// v17 and v18 are the only valid values; the @umbraco-cms/backoffice major
// version pinned in each lane's package.json determines which event class is
// available (UmbPropertyValueChangeEvent is v17-only; UmbChangeEvent is both).
const lane = (() => {
  const name = path.basename(UI_ROOT);
  if (name !== 'v17' && name !== 'v18') {
    throw new Error(`Unexpected lane directory '${name}'; expected v17 or v18.`);
  }
  return name;
})();

const resolveBuildVersion = (command: string, mode: string): string => {
  if (command !== 'build' || mode !== 'production') {
    return defaultBuildVersion;
  }

  const version = process.env[BUILD_VERSION_ENV_VAR]?.trim();
  if (version) {
    return version;
  }

  const isCiBuild = process.env.CI === 'true' || process.env.GITHUB_ACTIONS === 'true';
  if (isCiBuild) {
    throw new Error(
      `[version] Missing ${BUILD_VERSION_ENV_VAR} for production builds. ` +
        'Run the client build via MSBuild/dotnet pack or provide the variable explicitly.',
    );
  }

  console.warn(`[version] Missing ${BUILD_VERSION_ENV_VAR}; falling back to ${defaultBuildVersion}.`);
  return defaultBuildVersion;
};

// --- UTILS ---
const collectFiles = (dir: string, ext: string): string[] => {
  if (!existsSync(dir)) return [];
  const entries = readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name));
  return entries.flatMap((entry) => {
    if (entry.name.startsWith('.') || entry.name === 'dist') return [];
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) return collectFiles(fullPath, ext);
    return path.extname(entry.name).toLowerCase() === ext ? [fullPath] : [];
  });
};

// Helper to clean directories (rm -rf)
const cleanDir = async (dir: string) => {
  if (existsSync(dir)) {
    await rm(dir, { recursive: true, force: true });
  }
};

const copyVendorAssets = async () => {
  const assets = [
    ['@alpinejs/csp/dist/cdn.min.js', path.join(WEB_MARKDOWN, 'assets/vendor/alpine/alpine-csp.min.js')],
    ['tiny-markdown-editor/dist/tiny-mde.min.js', path.join(WEB_MARKDOWN, 'assets/vendor/tiny-mde/tiny-mde.min.js')],
    ['tiny-markdown-editor/dist/tiny-mde.min.css', path.join(WEB_MARKDOWN, 'assets/vendor/tiny-mde/tiny-mde.min.css')],
    ['material-design-lite/dist/material.min.js', path.join(WEB_MARKDOWN, 'assets/vendor/mdl/material.min.js')],
    [
      'material-design-lite/dist/material.pink-blue.min.css',
      path.join(WEB_MARKDOWN, 'assets/vendor/mdl/material.pink-blue.min.css'),
    ],
    [
      '@fontsource/material-icons/files/material-icons-latin-400-normal.woff2',
      path.join(WEB_MARKDOWN, 'assets/fonts/material-icons/MaterialIcons.woff2'),
    ],
    [
      '@fontsource/roboto/files/roboto-latin-300-normal.woff2',
      path.join(WEB_MARKDOWN, 'assets/fonts/roboto/Roboto-Light.woff2'),
    ],
    [
      '@fontsource/roboto/files/roboto-latin-400-normal.woff2',
      path.join(WEB_MARKDOWN, 'assets/fonts/roboto/Roboto-Regular.woff2'),
    ],
    [
      '@fontsource/roboto/files/roboto-latin-500-normal.woff2',
      path.join(WEB_MARKDOWN, 'assets/fonts/roboto/Roboto-Medium.woff2'),
    ],
    [
      '@fontsource/roboto/files/roboto-latin-700-normal.woff2',
      path.join(WEB_MARKDOWN, 'assets/fonts/roboto/Roboto-Bold.woff2'),
    ],
  ] as const;

  await Promise.all(
    assets.map(async ([source, destination]) => {
      await mkdir(path.dirname(destination), { recursive: true });
      return copyFile(require.resolve(source), destination);
    }),
  );
};

// --- PLUGIN: ASSET BUILDER (Themes + Markdown) ---
const sideCarAssetsPlugin = (): Plugin => {
  let mode = 'development';
  return {
    name: 'side-car-assets',
    configResolved(c) {
      mode = c.mode;
    },
    async buildStart() {
      const isProd = mode === 'production';
      const buildPromises: Promise<void>[] = [];

      await copyVendorAssets();

      // --- A. BUILD THEMES ---
      if (existsSync(WEB_THEMES)) {
        console.log(`[side-car] Scanning Themes...`);
        const dirs = readdirSync(WEB_THEMES, { withFileTypes: true }).filter(
          (d) => d.isDirectory() && !d.name.startsWith('.'),
        );

        for (const dir of dirs) {
          const themeName = dir.name;
          const assetsRoot = path.join(WEB_THEMES, themeName, 'assets');
          const srcDir = path.join(assetsRoot, 'src');
          const outDir = path.join(assetsRoot, 'dist');

          if (!existsSync(srcDir)) continue;

          // CLEAN: Wipe the dist folder before rebuilding
          await cleanDir(outDir);

          buildPromises.push(
            buildBundle({
              name: `${themeName} CSS`,
              inputs: collectFiles(srcDir, '.css'),
              output: path.join(outDir, 'css', `${themeName.toLowerCase()}.min.css`),
              type: 'css',
              isProd,
            }),
          );

          buildPromises.push(
            buildBundle({
              name: `${themeName} JS`,
              inputs: collectFiles(srcDir, '.js'),
              output: path.join(outDir, 'js', `${themeName.toLowerCase()}.min.js`),
              type: 'js',
              isProd,
            }),
          );
        }
      }

      // --- B. BUILD MARKDOWN EDITOR ---
      if (existsSync(WEB_MARKDOWN)) {
        console.log(`[side-car] Building Markdown Editor...`);
        const assetsRoot = path.join(WEB_MARKDOWN, 'assets');
        const outDir = path.join(assetsRoot, 'dist');
        const srcDir = path.join(assetsRoot, 'src');

        // CLEAN: Wipe dist folder
        await cleanDir(outDir);

        buildPromises.push(
          buildBundle({
            name: 'MD Editor CSS',
            inputs: collectFiles(srcDir, '.css'),
            output: path.join(outDir, 'css', 'md-editor.min.css'),
            type: 'css',
            isProd,
          }),
        );

        const entry = path.join(srcDir, 'js', 'md-editor.js');
        if (existsSync(entry)) {
          buildPromises.push(
            buildEsbuildBundle({
              name: 'MD Editor JS',
              entry,
              output: path.join(outDir, 'js', 'md-editor.min.js'),
              isProd,
            }),
          );
        }
      }

      await Promise.all(buildPromises);
    },
  };
};

// --- BUNDLE HELPERS ---
type BundleOptions = {
  name: string;
  inputs: string[];
  output: string;
  type: 'css' | 'js';
  isProd: boolean;
};

type EsbuildBundleOptions = {
  name: string;
  entry: string;
  output: string;
  isProd: boolean;
};

async function buildBundle({ name, inputs, output, type, isProd }: BundleOptions) {
  if (!inputs.length) return;

  let code = inputs.map((f: string) => readFileSync(f, 'utf8')).join('\n');

  if (type === 'css' && isProd) {
    code = code.replace(/^\uFEFF/, '').replace(/@charset\s+(['"]).*?\1\s*;/gi, '');
    const res = lightningcss.transform({
      filename: path.basename(output),
      code: Buffer.from(code),
      minify: true,
      targets: { chrome: 120, safari: 17, firefox: 120 },
    });
    code = res.code.toString();
  } else if (type === 'js') {
    const res = await esbuildBuild({
      stdin: { contents: code, resolveDir: path.dirname(inputs[0]), loader: 'js' },
      bundle: false,
      minify: isProd,
      write: false,
      target: 'es2020',
    });
    code = res.outputFiles[0].text;
  }

  await mkdir(path.dirname(output), { recursive: true });
  await writeFile(output, code);
  console.log(`  [${name}] Built`);
}

async function buildEsbuildBundle({ name, entry, output, isProd }: EsbuildBundleOptions) {
  const res = await esbuildBuild({
    entryPoints: [entry],
    bundle: true,
    minify: isProd,
    write: false,
    outfile: output,
    target: 'es2020',
    format: 'esm',
    sourcemap: isProd ? false : 'inline',
  });

  const code = res.outputFiles.find(
    (x: { path: string; text: string }) => x.path === output || x.path.endsWith('.js'),
  )?.text;
  if (code) {
    await mkdir(path.dirname(output), { recursive: true });
    await writeFile(output, code);
    console.log(`  [${name}] Built`);
  }
}

// --- PLUGIN: VERSIONING ---
const versioningPlugin = (): Plugin => {
  return {
    name: 'versioning',
    config: (_, c) => {
      buildVersion.value = resolveBuildVersion(c.command, c.mode);
      console.log(`[version] Using: ${buildVersion.value}`);
      return { define: { 'import.meta.env.APP_VERSION': JSON.stringify(buildVersion.value) } };
    },
  };
};

// --- PLUGIN: MANIFEST MOVER ---
const umbracoPackagePlugin = (): Plugin => {
  const MANIFEST = 'umbraco-package.json';
  return {
    name: 'manifest-mover',
    closeBundle: async () => {
      const src = path.join(UI_OUT, MANIFEST);
      const dest = path.join(PACKAGE_ROOT, MANIFEST);
      if (existsSync(src)) {
        await mkdir(path.dirname(dest), { recursive: true });
        let manifest: { version?: string };
        try {
          manifest = JSON.parse(readFileSync(src, 'utf8')) as { version?: string };
        } catch (error) {
          throw new Error(`[manifest] Invalid JSON in ${src}: ${String(error)}`);
        }
        manifest.version = buildVersion.value;
        await writeFile(dest, `${JSON.stringify(manifest, null, 2)}\n`);
        await unlink(src);
        console.log(`[manifest] Moved to ${dest}`);
      }
    },
  };
};

export default defineConfig(({ mode }: { mode: string }) => {
  const isProd = mode === 'production';
  return {
    root: UI_ROOT,
    publicDir: PUBLIC_ROOT,
    base: '/App_Plugins/Articulate/BackOffice/',
    resolve: {
      alias: {
        '@api': path.resolve(UI_ROOT, '../common/src/api', lane),
        '@lane': path.resolve(UI_ROOT, '../common/src/lane-adapter.ts'),
        'lit-html': path.resolve(UI_ROOT, 'node_modules/lit-html'),
      },
    },
    define: {
      // Build-time `LANE` constant. Set to the literal "v17" or "v18" via
      // esbuild's `define` replacement. common/src/lane-adapter.ts reads this
      // constant to dispatch to the per-lane event class.
      LANE: JSON.stringify(lane),
    },
    build: {
      outDir: UI_OUT,
      emptyOutDir: true, // This cleans the BackOffice UI folder automatically
      lib: {
        entry: UI_ENTRY,
        formats: ['es'],
        fileName: 'articulate-backoffice',
      },
      rollupOptions: { external: [/^@umbraco/] },
      sourcemap: true,
      minify: isProd ? 'esbuild' : false,
      cssMinify: isProd ? 'lightningcss' : false,
    },
    plugins: [sideCarAssetsPlugin(), versioningPlugin(), umbracoPackagePlugin()],
  };
});
