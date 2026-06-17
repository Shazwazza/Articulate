import { defineConfig, type Plugin } from "vite";
import path from "node:path";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { mkdir, writeFile, unlink, rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { build as esbuildBuild } from "esbuild";
import * as lightningcss from "lightningcss";

// --- CONSTANTS & PATHS ---
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// v17 lives under Client/, so WEB_ROOT is two levels up from this workspace folder.
const UI_ROOT = __dirname;
const UI_ENTRY = path.resolve(UI_ROOT, "src/main.ts");
const WEB_ROOT = path.resolve(UI_ROOT, "../..");
const UI_OUT = path.resolve(WEB_ROOT, "wwwroot/App_Plugins/Articulate/BackOffice");
const PACKAGE_ROOT = path.resolve(WEB_ROOT, "wwwroot/App_Plugins/Articulate");

// Static asset locations
const WEB_THEMES = path.resolve(WEB_ROOT, "wwwroot/App_Plugins/Articulate/Themes");
const WEB_MARKDOWN = path.resolve(WEB_ROOT, "wwwroot/App_Plugins/Articulate/MarkdownEditor");
const BUILD_VERSION_ENV_VAR = "ARTICULATE_APP_VERSION";
const PACKAGE_JSON_PATH = path.resolve(UI_ROOT, "package.json");

const defaultBuildVersion = (() => {
  try {
    const packageJson = JSON.parse(readFileSync(PACKAGE_JSON_PATH, "utf8")) as { version?: string };
    return packageJson.version?.trim() || "0.0.0-dev";
  } catch {
    return "0.0.0-dev";
  }
})();

const buildVersion = {
  value: defaultBuildVersion
};

const resolveBuildVersion = (command: string, mode: string): string => {
  if (command !== "build" || mode !== "production") {
    return defaultBuildVersion;
  }

  const version = process.env[BUILD_VERSION_ENV_VAR]?.trim();
  if (version) {
    return version;
  }

  const isCiBuild = process.env.CI === "true" || process.env.GITHUB_ACTIONS === "true";
  if (isCiBuild) {
    throw new Error(
      `[version] Missing ${BUILD_VERSION_ENV_VAR} for production builds. ` +
      "Run the client build via MSBuild/dotnet pack or provide the variable explicitly."
    );
  }

  console.warn(`[version] Missing ${BUILD_VERSION_ENV_VAR}; falling back to ${defaultBuildVersion}.`);
  return defaultBuildVersion;
};


// --- UTILS ---
const collectFiles = (dir: string, ext: string): string[] => {
  if (!existsSync(dir)) return [];
  const entries = readdirSync(dir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name));
  return entries.flatMap(entry => {
    if (entry.name.startsWith(".") || entry.name === "dist") return [];
    const fullPath = path.join(dir, entry.name);
    return entry.isDirectory()
      ? collectFiles(fullPath, ext)
      : (path.extname(entry.name).toLowerCase() === ext ? [fullPath] : []);
  });
};

// Helper to clean directories (rm -rf)
const cleanDir = async (dir: string) => {
  if (existsSync(dir)) {
    await rm(dir, { recursive: true, force: true });
  }
};

// --- PLUGIN: ASSET BUILDER (Themes + Markdown) ---
const sideCarAssetsPlugin = (): Plugin => {
  let mode = "development";
  return {
    name: 'side-car-assets',
    configResolved(c) { mode = c.mode; },
    async buildStart() {
      const isProd = mode === "production";
      const buildPromises: Promise<void>[] = [];

      // --- A. BUILD THEMES ---
      if (existsSync(WEB_THEMES)) {
        console.log(`[side-car] Scanning Themes...`);
        const dirs = readdirSync(WEB_THEMES, { withFileTypes: true })
          .filter(d => d.isDirectory() && !d.name.startsWith("."));

        for (const dir of dirs) {
          const themeName = dir.name;
          const assetsRoot = path.join(WEB_THEMES, themeName, "assets");
          const srcDir = path.join(assetsRoot, "src");
          const vendorDir = path.join(assetsRoot, "vendor");
          const outDir = path.join(assetsRoot, "dist");

          if (!existsSync(srcDir)) continue;

          // CLEAN: Wipe the dist folder before rebuilding
          await cleanDir(outDir);

          buildPromises.push(
            buildBundle({
              name: `${themeName} CSS`,
              inputs: [...collectFiles(vendorDir, ".css"), ...collectFiles(srcDir, ".css")],
              output: path.join(outDir, "css", `${themeName.toLowerCase()}.min.css`),
              type: 'css', isProd
            })
          );

          buildPromises.push(
            buildBundle({
              name: `${themeName} JS`,
              inputs: [...collectFiles(vendorDir, ".js"), ...collectFiles(srcDir, ".js")],
              output: path.join(outDir, "js", `${themeName.toLowerCase()}.min.js`),
              type: 'js', isProd
            })
          );
        }
      }

      // --- B. BUILD MARKDOWN EDITOR ---
      if (existsSync(WEB_MARKDOWN)) {
        console.log(`[side-car] Building Markdown Editor...`);
        const assetsRoot = path.join(WEB_MARKDOWN, "assets");
        const outDir = path.join(assetsRoot, "dist");
        const srcDir = path.join(assetsRoot, "src");

        // CLEAN: Wipe dist folder
        await cleanDir(outDir);

        buildPromises.push(
          buildBundle({
            name: "MD Editor CSS",
            inputs: collectFiles(assetsRoot, ".css"),
            output: path.join(outDir, "css", "md-editor.min.css"),
            type: 'css', isProd
          })
        );

        const entry = path.join(srcDir, "js", "md-editor.js");
        if (existsSync(entry)) {
          buildPromises.push(
            buildEsbuildBundle({
              name: "MD Editor JS",
              entry,
              output: path.join(outDir, "js", "md-editor.min.js"),
              isProd
            })
          );
        }
      }

      await Promise.all(buildPromises);
    }
  }
};

// --- BUNDLE HELPERS ---
async function buildBundle({ name, inputs, output, type, isProd }: any) {
  if (!inputs.length) return;

  let code = inputs.map((f: string) => readFileSync(f, "utf8")).join("\n");

  if (type === 'css' && isProd) {
    code = code.replace(/^\uFEFF/, "").replace(/@charset\s+(['"]).*?\1\s*;/gi, "");
    const res = lightningcss.transform({
      filename: path.basename(output),
      code: Buffer.from(code),
      minify: true,
      targets: { chrome: 120, safari: 17, firefox: 120 }
    });
    code = res.code.toString();
  } else if (type === 'js') {
    const res = await esbuildBuild({
      stdin: { contents: code, resolveDir: path.dirname(inputs[0]), loader: 'js' },
      bundle: false,
      minify: isProd,
      write: false,
      target: "es2020"
    });
    code = res.outputFiles[0].text;
  }

  await mkdir(path.dirname(output), { recursive: true });
  await writeFile(output, code);
  console.log(`  [${name}] Built`);
}

async function buildEsbuildBundle({ name, entry, output, isProd }: any) {
  const res = await esbuildBuild({
    entryPoints: [entry],
    bundle: true,
    minify: isProd,
    write: false,
    outfile: output,
    target: "es2020",
    format: "esm",
    sourcemap: isProd ? false : "inline"
  });

  const code = res.outputFiles.find(x => x.path === output || x.path.endsWith('.js'))?.text;
  if (code) {
    await mkdir(path.dirname(output), { recursive: true });
    await writeFile(output, code);
    console.log(`  [${name}] Built`);
  }
}

// --- PLUGIN: VERSIONING ---
const versioningPlugin = (): Plugin => {
  return {
    name: "versioning",
    config: (_, c) => {
      buildVersion.value = resolveBuildVersion(c.command, c.mode);
      console.log(`[version] Using: ${buildVersion.value}`);
      return { define: { "import.meta.env.APP_VERSION": JSON.stringify(buildVersion.value) } };
    }
  };
};

// --- PLUGIN: MANIFEST MOVER ---
const umbracoPackagePlugin = (): Plugin => {
  const MANIFEST = "umbraco-package.json";
  return {
    name: "manifest-mover",
    closeBundle: async () => {
      const src = path.join(UI_OUT, MANIFEST);
      const dest = path.join(PACKAGE_ROOT, MANIFEST);
      if (existsSync(src)) {
        await mkdir(path.dirname(dest), { recursive: true });
        const manifest = JSON.parse(readFileSync(src, "utf8")) as { version?: string };
        manifest.version = buildVersion.value;
        await writeFile(dest, `${JSON.stringify(manifest, null, 2)}\n`);
        await unlink(src);
        console.log(`[manifest] Moved to ${dest}`);
      }
    }
  };
};

export default defineConfig(({ mode }) => {
  const isProd = mode === "production";
  return {
    root: UI_ROOT,
    base: "/App_Plugins/Articulate/BackOffice/",
    build: {
      outDir: UI_OUT,
      emptyOutDir: true, // This cleans the BackOffice UI folder automatically
      lib: {
        entry: UI_ENTRY,
        formats: ["es"],
        fileName: "articulate-backoffice"
      },
      rollupOptions: { external: [/^@umbraco/] },
      sourcemap: true,
      minify: isProd ? "esbuild" : false,
      cssMinify: isProd ? 'lightningcss' : false,
    },
    plugins: [
      sideCarAssetsPlugin(),
      versioningPlugin(),
      umbracoPackagePlugin()
    ]
  };
});
