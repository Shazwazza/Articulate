#!/usr/bin/env node
//
// smoke-package.mjs — verify Articulate .nupkg / .snupkg contents
//
// Run in CI after `dotnet pack` to catch regressions before artifact upload.
// Checks file presence, .nuspec structure, umbraco-package.json structure,
// embedded resources in the shipped DLLs, and absence of known-bad content
// (e.g. lock files leaking into the package).
//
// Usage:
//   node build/smoke-package.mjs build/Release/v17 [build/Release/v18 ...]
//   node build/smoke-package.mjs path/to/Articulate.6.1.0.nupkg
//
// Exits 0 if all checks pass, 1 otherwise.

import { spawnSync } from "node:child_process";
import {
	mkdtempSync,
	readFileSync,
	readdirSync,
	rmSync,
	statSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { basename, extname, join } from "node:path";

const args = process.argv.slice(2);
if (args.length === 0) {
	console.error("Usage: node build/smoke-package.mjs <dir|file> [...]");
	process.exit(2);
}

// ---------------------------------------------------------------------------
// unzip helpers
// ---------------------------------------------------------------------------

function unzipList(file) {
	// Returns [{ name, size, ... }] from `unzip -l`.
	const result = spawnSync("unzip", ["-l", file], { encoding: "utf8" });
	if (result.status !== 0)
		throw new Error(`unzip -l ${file} failed: ${result.stderr}`);
	return result.stdout;
}

function unzipExtract(file, entries, dest) {
	// entries: array of paths inside the archive to extract
	const args = ["-o", "-q", file, ...entries, "-d", dest];
	const result = spawnSync("unzip", args, { encoding: "utf8" });
	if (result.status !== 0)
		throw new Error(`unzip -o ${file} failed: ${result.stderr}`);
}

function parseUnzipList(stdout) {
	// Parses `unzip -l` output. Returns array of { name, size }.
	const lines = stdout.split("\n");
	const entries = [];
	let inTable = false;
	for (const line of lines) {
		if (line.startsWith("---")) {
			inTable = !inTable;
			continue;
		}
		if (!inTable) continue;
		// Format: "  Length      Date    Time    Name"
		const match = line.match(/^\s+(\d+)\s+\S+\s+\S+\s+(.+?)\s*$/);
		if (match) entries.push({ size: Number(match[1]), name: match[2] });
	}
	return entries;
}

function namesMatching(entries, regex) {
	return entries.filter((e) => regex.test(e.name)).map((e) => e.name);
}

function hasAny(entries, predicate) {
	return entries.some(predicate);
}

// ---------------------------------------------------------------------------
// checks
// ---------------------------------------------------------------------------

let checks = 0;
let failures = 0;
const failuresByPackage = new Map();

function expect(label, condition, detail = "") {
	checks++;
	if (condition) {
		console.log(`  ok  ${label}`);
	} else {
		console.log(`  FAIL ${label}${detail ? "  " + detail : ""}`);
		failures++;
		failuresByPackage.set(
			currentPackage,
			(failuresByPackage.get(currentPackage) ?? 0) + 1,
		);
	}
}

function checkGroup(name, fn) {
	console.log(`\n${currentPackage} :: ${name}`);
	fn();
}

// -- per-package dispatch ---------------------------------------------------

let currentPackage = "";

function checkPackage(file) {
	currentPackage = basename(file);
	const isSymbols = extname(file) === ".snupkg";
	const entries = parseUnzipList(unzipList(file));
	const names = entries.map((e) => e.name);

	// We need a temp dir to extract DLLs/etc. for resource inspection.
	const work = mkdtempSync(join(tmpdir(), "smoke-pkg-"));

	try {
		if (isSymbols) {
			checkSymbols(file, entries, names, work);
		} else if (currentPackage.startsWith("Articulate.Theme.Sample.")) {
			checkSamplePackage(file, entries, names, work);
		} else if (currentPackage.startsWith("Articulate.")) {
			checkMainPackage(file, entries, names, work);
		} else {
			console.log(`\n${currentPackage} :: skipped (unknown package id)`);
		}
	} finally {
		rmSync(work, { recursive: true, force: true });
	}
}

// -- Articulate.{ver}.nupkg -----------------------------------------------

function checkMainPackage(file, entries, names, work) {
	checkGroup("root files", () => {
		expect("LICENSE at package root", names.includes("LICENSE"));
		expect("README.md at package root", names.includes("README.md"));
		expect("icon.png at package root", names.includes("icon.png"));
	});

	checkGroup("no fluff", () => {
		const locks = namesMatching(entries, /packages\..*\.lock\.json$/);
		expect(
			"no packages.*.lock.json shipped",
			locks.length === 0,
			locks.length ? `(found: ${locks.join(", ")})` : "",
		);
	});

	checkGroup(".nuspec", () => {
		const nuspecEntries = names.filter((n) => n.endsWith(".nuspec"));
		expect("exactly one .nuspec", nuspecEntries.length === 1);
		const nuspecName = nuspecEntries[0];
		if (!nuspecName) return;
		unzipExtract(file, [nuspecName], work);
		const nuspec = readFileSync(join(work, nuspecName), "utf8");
		expect("id=Articulate", /<id>Articulate<\/id>/.test(nuspec));
		expect(
			"has net10.0 dependency group",
			/targetFramework="net10\.0"/.test(nuspec),
		);
		expect(
			"depends on Umbraco.Cms.Web.Website",
			/<dependency id="Umbraco\.Cms\.Web\.Website"/.test(nuspec),
		);
		expect(
			"depends on Umbraco.Cms.Api.Management",
			/<dependency id="Umbraco\.Cms\.Api\.Management"/.test(nuspec),
		);
		expect("declares contentFiles", /<contentFiles>/.test(nuspec));
	});

	checkGroup("lib/net10.0", () => {
		expect(
			"Articulate.Web.dll present",
			names.includes("lib/net10.0/Articulate.Web.dll"),
		);
		expect(
			"Articulate.Web.xml present",
			names.includes("lib/net10.0/Articulate.Web.xml"),
		);
		expect(
			"Articulate.dll present (from IncludeProjectReferenceDlls)",
			names.includes("lib/net10.0/Articulate.dll"),
		);
		expect(
			"Articulate.xml present",
			names.includes("lib/net10.0/Articulate.xml"),
		);
	});

	checkGroup("build/", () => {
		expect(
			"Articulate.targets present",
			names.includes("build/Articulate.targets"),
		);
		expect(
			"Articulate.props present",
			names.includes("build/Articulate.props"),
		);
		expect(
			"StaticWebAssets props present",
			hasAny(entries, (e) =>
				/^build\/Microsoft\.AspNetCore\.StaticWebAssets/.test(e.name),
			),
		);
	});

	checkGroup("umbraco-package.json", () => {
		// May be at staticwebassets/... (modern SDK) or contentFiles/any/{tfm}/...
		// (legacy). Accept either.
		const manifest = names.find((n) => n.endsWith("/umbraco-package.json"));
		expect("manifest present", !!manifest);
		if (!manifest) return;
		unzipExtract(file, [manifest], work);
		const json = JSON.parse(readFileSync(join(work, manifest), "utf8"));
		expect(
			"manifest has id",
			typeof json.id === "string" && json.id.length > 0,
		);
		expect(
			"manifest has name",
			typeof json.name === "string" && json.name.length > 0,
		);
		expect(
			"manifest has version",
			typeof json.version === "string" && json.version.length > 0,
		);
		expect("manifest has extensions array", Array.isArray(json.extensions));
		const backOfficeLike = (json.extensions ?? []).some(
			(e) =>
				e.type === "backOffice" || e.type === "bundle" || e.js || e.element,
		);
		expect("manifest declares a back-office entry", backOfficeLike);
	});

	checkGroup("BackOffice bundles", () => {
		const bo = names.filter((n) =>
			n.includes("/App_Plugins/Articulate/BackOffice/"),
		);
		expect(
			"BackOffice folder populated",
			bo.length >= 5,
			`(found ${bo.length} files)`,
		);
		expect(
			"articulate-backoffice.js present",
			bo.some((n) => n.endsWith("/articulate-backoffice.js")),
		);
		expect(
			"dashboard bundle present",
			bo.some((n) => /\/dashboard\.element-.*\.js$/.test(n)),
		);
		expect(
			"theme-picker bundle present",
			bo.some((n) => /\/theme-picker\.element-.*\.js$/.test(n)),
		);
		expect(
			"markdown editor bundle present",
			bo.some((n) =>
				/\/property-editor-ui-markdown-editor\.element-.*\.js$/.test(n),
			),
		);
		expect(
			"entrypoint bundle present",
			bo.some((n) => /\/entrypoint-.*\.js$/.test(n)),
		);
		const previews = ["material", "mini", "phantom", "vapor"].map(
			(t) => `theme-${t}.png`,
		);
		for (const p of previews) {
			expect(
				`backoffice preview ${p} present`,
				bo.some((n) => n.endsWith(`/assets/${p}`)),
			);
		}
	});

	checkGroup("MarkdownEditor assets", () => {
		const me = (suffix) =>
			names.some(
				(n) =>
					n.includes("/App_Plugins/Articulate/MarkdownEditor/") &&
					n.endsWith(suffix),
			);
		expect(
			"md-editor.min.css present",
			me("/assets/dist/css/md-editor.min.css"),
		);
		expect("md-editor.min.js present", me("/assets/dist/js/md-editor.min.js"));
	});

	checkGroup("Themes", () => {
		const required = [
			["Material", "css/material.min.css", "js/material.min.js"],
			["Mini", "css/mini.min.css", "js/mini.min.js"],
			["Phantom", "css/phantom.min.css", null], // image-only theme
			["VAPOR", "css/vapor.min.css", "js/vapor.min.js"],
		];
		for (const [theme, css, js] of required) {
			const base = `App_Plugins/Articulate/Themes/${theme}/assets/dist/`;
			expect(
				`${theme} theme css present`,
				names.some(
					(n) =>
						n.includes(`/App_Plugins/Articulate/Themes/${theme}/`) &&
						n.endsWith(base + css),
				),
			);
			if (js) {
				expect(
					`${theme} theme js present`,
					names.some(
						(n) =>
							n.includes(`/App_Plugins/Articulate/Themes/${theme}/`) &&
							n.endsWith(base + js),
					),
				);
			}
		}
	});

	checkGroup("embedded resources in Articulate.dll", () => {
		const dllPath = "lib/net10.0/Articulate.dll";
		if (!names.includes(dllPath)) {
			expect("Articulate.dll extractable", false);
			return;
		}
		unzipExtract(file, [dllPath], work);
		const dll = readFileSync(join(work, dllPath));
		// Manifest resource names start with "Articulate.Packaging." (default
		// EmbeddedResource namespace from project root).
		const expected = [
			"Articulate.Packaging.author.jpg",
			"Articulate.Packaging.banner.jpg",
			"Articulate.Packaging.logo.png",
			"Articulate.Packaging.package.zip",
			"Articulate.Packaging.post1.jpg",
			"Articulate.Packaging.post2.jpg",
		];
		const text = dll.toString("latin1");
		for (const name of expected) {
			expect(
				`resource "${name}" present in Articulate.dll`,
				text.includes(name),
			);
		}
	});

	checkGroup("embedded theme + razor in Articulate.Web.dll", () => {
		const dllPath = "lib/net10.0/Articulate.Web.dll";
		if (!names.includes(dllPath)) {
			expect("Articulate.Web.dll extractable", false);
			return;
		}
		unzipExtract(file, [dllPath], work);
		const dll = readFileSync(join(work, dllPath));
		const text = dll.toString("latin1");
		// LogicalName prefix used by ArticulateThemeRepository.GetManifestResourceStream
		expect(
			"Articulate.Theme:// prefix present (theme copy reads it)",
			text.includes("Articulate.Theme://"),
		);
		// Generated razor view class names: App_Plugins_Articulate_Themes_*.
		// We expect at least 50 distinct compiled views across the four themes.
		const razorMatches = text.match(/App_Plugins_Articulate_Themes/g) ?? [];
		expect(
			"razor-compiled theme views present (>=50 occurrences)",
			razorMatches.length >= 50,
			`(found ${razorMatches.length})`,
		);
	});
}

// -- Articulate.Theme.Sample.{ver}.nupkg -----------------------------------

function checkSamplePackage(file, entries, names, work) {
	checkGroup("root files", () => {
		expect("LICENSE at package root", names.includes("LICENSE"));
		expect("README.md at package root", names.includes("README.md"));
		expect("icon.png at package root", names.includes("icon.png"));
	});

	checkGroup("no fluff", () => {
		const locks = namesMatching(entries, /packages\..*\.lock\.json$/);
		expect(
			"no packages.*.lock.json shipped",
			locks.length === 0,
			locks.length ? `(found: ${locks.join(", ")})` : "",
		);
	});

	checkGroup(".nuspec", () => {
		const nuspecEntries = names.filter((n) => n.endsWith(".nuspec"));
		expect("exactly one .nuspec", nuspecEntries.length === 1);
		const nuspecName = nuspecEntries[0];
		if (!nuspecName) return;
		unzipExtract(file, [nuspecName], work);
		const nuspec = readFileSync(join(work, nuspecName), "utf8");
		expect(
			"id=Articulate.Theme.Sample",
			/<id>Articulate\.Theme\.Sample<\/id>/.test(nuspec),
		);
		expect("depends on Articulate", /<dependency id="Articulate"/.test(nuspec));
	});

	checkGroup("lib/net10.0", () => {
		expect(
			"Articulate.Theme.Sample.dll present",
			names.includes("lib/net10.0/Articulate.Theme.Sample.dll"),
		);
	});

	checkGroup("static web assets", () => {
		const sampleAssets = names.filter((n) =>
			n.includes("/App_Plugins/Articulate/Themes/Sample/"),
		);
		expect("Sample theme assets folder populated", sampleAssets.length >= 2);
		expect(
			"site.css present",
			sampleAssets.some((n) => n.endsWith("/assets/css/site.css")),
		);
		expect(
			"site.js present",
			sampleAssets.some((n) => n.endsWith("/assets/js/site.js")),
		);
	});
}

// -- Articulate.{ver}.snupkg -----------------------------------------------

function checkSymbols(_file, entries, names, _work) {
	checkGroup("symbols package", () => {
		expect(
			"Articulate.Web.pdb present",
			names.includes("lib/net10.0/Articulate.Web.pdb"),
		);
		const pdb = entries.find(
			(e) => e.name === "lib/net10.0/Articulate.Web.pdb",
		);
		if (pdb) {
			expect(
				"Articulate.Web.pdb is non-trivial (> 50 KB)",
				pdb.size > 50_000,
				`(size ${pdb.size})`,
			);
		}
	});
}

// ---------------------------------------------------------------------------
// main
// ---------------------------------------------------------------------------

const files = [];
for (const arg of args) {
	const stat = statSync(arg);
	if (stat.isDirectory()) {
		// Pick up .nupkg + .snupkg; sort so .nupkg comes before .snupkg per id.
		const found = readdirSync(arg)
			.filter((n) => /\.(nu|snu)pkg$/.test(n))
			.sort()
			.map((n) => join(arg, n));
		files.push(...found);
	} else {
		files.push(arg);
	}
}

for (const file of files) {
	try {
		checkPackage(file);
	} catch (err) {
		failures++;
		console.log(`\n${basename(file)} :: error ${err.message}`);
	}
}

console.log(
	`\n${files.length} package(s), ${checks} checks, ${failures} failure(s)`,
);
if (failuresByPackage.size > 0) {
	for (const [pkg, count] of failuresByPackage) {
		console.log(`  ${pkg}: ${count} failure(s)`);
	}
}
process.exit(failures > 0 ? 1 : 0);
