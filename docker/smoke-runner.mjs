#!/usr/bin/env node

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const repo = fileURLToPath(new URL("..", import.meta.url));
const runner = readFileSync(new URL("run.cs", import.meta.url), "utf8");
const buildRunner = readFileSync(new URL("../build/build.cs", import.meta.url), "utf8");
const smoke = readFileSync(new URL("smoke.mjs", import.meta.url), "utf8");

function run(...args) {
    return spawnSync(
        "dotnet",
        ["run", "docker/run.cs", "--", ...args],
        { cwd: repo, encoding: "utf8" },
    );
}

function command(name, nextName) {
    const start = runner.indexOf(`async Task<int> ${name}`);
    const end = runner.indexOf(`\nasync Task<int> ${nextName}`, start + 1);
    assert.notEqual(start, -1, `${name} is missing`);
    assert.notEqual(end, -1, `${name} boundary is missing`);
    return runner.slice(start, end);
}

const help = run("help");
assert.equal(help.status, 0, help.stderr);
assert.match(help.stdout, /docker-down/);

const partialHelp = run("help", "docker");
assert.equal(partialHelp.status, 1);
assert.match(partialHelp.stderr, /No help topic 'docker'/);

const valuedFlag = run("docker-test", "--keep", "true");
assert.equal(valuedFlag.status, 1);
assert.match(valuedFlag.stderr, /--keep is a flag and does not accept a value/);

const destructiveFlags = run("docker-down", "--volumes", "--purge");
assert.equal(destructiveFlags.status, 1);
assert.match(destructiveFlags.stderr, /--volumes and --purge cannot be used together/);

const composeFailure = spawnSync(
    "dotnet",
    [
        "run",
        "docker/run.cs",
        "--",
        "docker-down",
        "--lane",
        "all",
    ],
    {
        cwd: repo,
        encoding: "utf8",
        env: { ...process.env, DOCKER_HOST: "tcp://127.0.0.1:1" },
    },
);
assert.equal(composeFailure.status, 1);
assert.doesNotMatch(composeFailure.stderr, /Unexpected argument '-f'/);
assert.doesNotMatch(composeFailure.stderr, /--lane must be v17 or v18/);

assert.match(runner, /build\/build\.cs/);
assert.doesNotMatch(buildRunner, /docker-/i);
assert.match(command("DockerBuild", "DockerDev"), /EnsurePackages/);
assert.match(command("DockerDev", "DockerProd"), /EnsurePackages/);
assert.doesNotMatch(command("DockerDown", "DockerStatus"), /allowFailure/);
assert.doesNotMatch(command("DockerProd", "DockerDown"), /EnsurePackages/);
assert.match(smoke, /unpublished \+= await confirmChildren\(base, token, item\.id/);

console.log("Docker runner smoke checks passed.");
