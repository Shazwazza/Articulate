#!/usr/bin/env -S dotnet --
#:property NoWarn=SA1400,SA1503,SA1519,SA1116,SA1117,SA1122,SA1649,IDE0008,IDE0011,IDE0040,SA1500,IL2026,IL3050
#:property JsonSerializerIsReflectionEnabledByDefault=true

#nullable enable

// Prepare a target-branch candidate from selected commits in the side-by-side
// development branch. This script never pushes or opens a pull request.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

try
{
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
        Help();
        return 0;
    }

    var command = args[0];
    var opts = Opts.Parse(args[1..]);
    return command switch
    {
        "patch" => await PatchAsync(opts.Validate(command, "profile", "base", "source", "commits", "branch", "worktree", "manifest", "skip-build", "docker")),
        _ => throw new ArgumentException($"Unknown command '{command}'. Run 'dotnet run --file build/promote.cs -- --help'.")
    };
}
catch (Exception e)
{
    Console.Error.WriteLine($"ERROR: {e.Message}");
    return 1;
}

void Help()
{
    var path = Path.Combine(Env.Repo, "build", "promote-help.md");
    Console.WriteLine(File.ReadAllText(path));
}

async Task<int> PatchAsync(Opts o)
{
    var profile = o.Profile();
    var baseRef = o.Required("base");
    var sourceRef = o.Required("source");
    var commits = o.Commits();
    var baseSha = await GitCapture("rev-parse", "--verify", $"{baseRef}^{{commit}}");
    var sourceSha = await GitCapture("rev-parse", "--verify", $"{sourceRef}^{{commit}}");

    foreach (var commit in commits)
    {
        await Git("merge-base", "--is-ancestor", commit, sourceSha);
    }

    var shortSource = await GitCapture("rev-parse", "--short", sourceSha);
    var branch = o.String("branch") ?? $"promote/{profile}/{shortSource}";
    ValidateBranchName(branch);
    await Git("check-ref-format", "--branch", branch);
    if (await GitExitCode("show-ref", "--verify", "--quiet", $"refs/heads/{branch}") == 0)
        throw new InvalidOperationException($"Local branch '{branch}' already exists. Choose --branch explicitly or remove it first.");

    var worktree = o.String("worktree")
        ?? Path.Combine(Path.GetTempPath(), $"articulate-promote-{Guid.NewGuid():N}");
    if (Directory.Exists(worktree) || File.Exists(worktree))
        throw new InvalidOperationException($"Worktree path already exists: {worktree}");

    await Git("worktree", "add", "--detach", worktree, baseSha);
    var candidateCreated = false;
    try
    {
        await GitAt(worktree, "switch", "-c", branch);
        candidateCreated = true;

        Console.WriteLine($"Promoting {string.Join(", ", commits)} to {profile}.");
        Console.WriteLine($"Base:   {baseRef} ({baseSha})");
        Console.WriteLine($"Source: {sourceRef} ({sourceSha})");
        Console.WriteLine($"Branch: {branch}");

        try
        {
            await GitAt(worktree, new[] { "cherry-pick", "--no-edit" }.Concat(commits).ToArray());
        }
        catch
        {
            await RunProcessAt(worktree, "git", new[] { "cherry-pick", "--abort" }, allowFailure: true);
            throw new InvalidOperationException($"Cherry-pick failed. No commits were applied; inspect the candidate worktree at '{worktree}'.");
        }

        var forbidden = await FindForbiddenReferences(worktree, profile);
        if (forbidden.Count > 0)
        {
            Console.Error.WriteLine("Forbidden target references:");
            foreach (var line in forbidden) Console.Error.WriteLine(line);
            throw new InvalidOperationException($"{profile} target checks failed.");
        }

        var nativeE2ETested = false;
        if (!o.Flag("skip-build"))
        {
            await RunDockerUtilityCompileCheck(worktree);
            await RunTargetBuild(worktree);
            await RunPackageSmoke(worktree);
            if (File.Exists(Path.Combine(worktree, "build", "test.cs")))
            {
                Console.WriteLine("Running fresh packaged-site native E2E tests");
                await RunAt(worktree, "dotnet", "run", "--file", "build/test.cs", "--", "fresh");
                nativeE2ETested = true;
            }
            if (o.Flag("docker"))
                await RunDockerTest(worktree);
        }

        var candidateSha = await GitCaptureAt(worktree, "rev-parse", "HEAD");
        var treeSha = await GitCaptureAt(worktree, "rev-parse", "HEAD^{tree}");
        var manifest = new PromotionManifest(
            Tool: "build/promote.cs",
            Profile: profile,
            BaseRef: baseRef,
            BaseSha: baseSha,
            SourceRef: sourceRef,
            SourceSha: sourceSha,
            Commits: commits,
            CandidateBranch: branch,
            CandidateSha: candidateSha,
            TreeSha: treeSha,
            Worktree: worktree,
            BuildSkipped: o.Flag("skip-build"),
            DockerTested: o.Flag("docker"),
            NativeE2ETested: nativeE2ETested);

        var manifestPath = o.String("manifest");
        if (manifestPath is not null)
        {
            var resolvedManifest = Path.GetFullPath(manifestPath, Env.Repo);
            Directory.CreateDirectory(Path.GetDirectoryName(resolvedManifest)!);
            await File.WriteAllTextAsync(resolvedManifest, JsonSerializer.Serialize(manifest, jsonOptions));
            Console.WriteLine($"Manifest: {resolvedManifest}");
        }

        Console.WriteLine();
        Console.WriteLine("Promotion passed.");
        Console.WriteLine($"Review worktree: {worktree}");
        Console.WriteLine($"Push candidate:  git -C \"{worktree}\" push origin {branch}");
        Console.WriteLine($"Open the PR against the maintainer-selected target branch.");
        return 0;
    }
    catch
    {
        if (!candidateCreated)
            await RunProcessAt(Env.Repo, "git", new[] { "worktree", "remove", "--force", worktree }, allowFailure: true);
        throw;
    }
}

async Task RunTargetBuild(string worktree)
{
    var buildScript = Path.Combine(worktree, "build", "build.cs");
    if (!File.Exists(buildScript))
        throw new InvalidOperationException($"Target worktree has no build script: {buildScript}");

    await RunAt(worktree, "dotnet", "run", "--file", "build/build.cs", "--", "build", "--client", "true", "--tests", "--sample", "--clean");
}

async Task RunPackageSmoke(string worktree)
{
    var smokeScript = Path.Combine(worktree, "build", "smoke-package.mjs");
    if (!File.Exists(smokeScript))
        throw new InvalidOperationException($"Target worktree has no package smoke test: {smokeScript}");

    var releaseRoot = Path.Combine(worktree, "build", "Release");
    var packageDirs = Directory.Exists(releaseRoot)
        ? Directory.EnumerateDirectories(releaseRoot)
            .Where(path => Directory.EnumerateFiles(path, "Articulate.*.nupkg").Any())
            .ToArray()
        : Array.Empty<string>();
    if (packageDirs.Length == 0 && Directory.Exists(releaseRoot) && Directory.EnumerateFiles(releaseRoot, "Articulate.*.nupkg").Any())
        packageDirs = new[] { releaseRoot };
    if (packageDirs.Length == 0)
        throw new InvalidOperationException($"No package directory found under '{releaseRoot}'.");

    await RunAt(worktree, "node", new[] { "build/smoke-package.mjs" }.Concat(packageDirs.Select(Path.GetFullPath)).ToArray());
}

async Task RunDockerUtilityCompileCheck(string worktree)
{
    if (!File.Exists(Path.Combine(worktree, "docker", "run.cs")))
        return; // target carries no docker stack
    Console.WriteLine("Compile-checking docker/run.cs");
    await RunAt(worktree, "dotnet", "run", "--file", "docker/run.cs");
}

async Task RunDockerTest(string worktree)
{
    var runScript = Path.Combine(worktree, "docker", "run.cs");
    if (!File.Exists(runScript))
        throw new InvalidOperationException($"--docker requested but the target worktree has no docker stack: {runScript}");
    Console.WriteLine("Running docker runtime validation (docker-test)");
    await RunAt(worktree, "dotnet", "run", "--file", "docker/run.cs", "--", "docker-test");
}

async Task<List<string>> FindForbiddenReferences(string worktree, string profile)
{
    var pattern = profile switch
    {
        "v17-lts" => "UMBRACO_18_OR_GREATER|version-v18|packages[.]v18|[\\\\/]v18([\\\\/]|[.]|$)|[\\\\/]V18([\\\\/]|[.]|$)|ArticulatePackageLane|articulate-backoffice-v18|--lane v18",
        "v18-sts" => "UMBRACO_17_OR_GREATER|version-v17|packages[.]v17|[\\\\/]v17([\\\\/]|[.]|$)|[\\\\/]V17([\\\\/]|[.]|$)|articulate-backoffice-v17|--lane v17",
        _ => throw new ArgumentException($"Unknown promotion profile '{profile}'.")
    };

    var output = await CaptureProcessAt(worktree, "git", new[] { "grep", "-n", "-I", "-E", pattern, "--", ":!build/promote.cs", ":!build/promote-help.md" }, allowFailure: true);
    return string.IsNullOrWhiteSpace(output)
        ? new List<string>()
        : output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
}

void ValidateBranchName(string branch)
{
    if (branch.StartsWith('-') || branch.Contains("..", StringComparison.Ordinal) || branch.Contains(' '))
        throw new ArgumentException($"Invalid candidate branch name '{branch}'.");
}

async Task Git(params string[] args)
    => await RunAt(Env.Repo, "git", args);

async Task<int> GitExitCode(params string[] args)
    => await RunExitCodeAt(Env.Repo, "git", args);

async Task<string> GitCapture(params string[] args)
    => await CaptureAt(Env.Repo, "git", args);

async Task GitAt(string cwd, params string[] args)
    => await RunAt(cwd, "git", args);

async Task<string> GitCaptureAt(string cwd, params string[] args)
    => await CaptureAt(cwd, "git", args);

async Task RunAt(string cwd, string file, params string[] args)
    => await RunProcessAt(cwd, file, args, allowFailure: false);

async Task RunProcessAt(string cwd, string file, IEnumerable<string> args, bool allowFailure = false)
{
    Env.Require(file);
    var psi = new ProcessStartInfo(file)
    {
        UseShellExecute = false,
        WorkingDirectory = cwd,
    };
    foreach (var arg in args) psi.ArgumentList.Add(arg);
    Console.WriteLine($"> {file} {string.Join(' ', args)}");
    using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}.");
    await process.WaitForExitAsync();
    if (process.ExitCode != 0 && !allowFailure)
        throw new InvalidOperationException($"{file} exited {process.ExitCode}.");
}

async Task<int> RunExitCodeAt(string cwd, string file, params string[] args)
{
    Env.Require(file);
    var psi = new ProcessStartInfo(file)
    {
        UseShellExecute = false,
        WorkingDirectory = cwd,
    };
    foreach (var arg in args) psi.ArgumentList.Add(arg);
    using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}.");
    await process.WaitForExitAsync();
    return process.ExitCode;
}

async Task<string> CaptureAt(string cwd, string file, params string[] args)
    => await CaptureProcessAt(cwd, file, args, allowFailure: false);

async Task<string> CaptureProcessAt(string cwd, string file, IEnumerable<string> args, bool allowFailure)
{
    Env.Require(file);
    var psi = new ProcessStartInfo(file)
    {
        UseShellExecute = false,
        WorkingDirectory = cwd,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (var arg in args) psi.ArgumentList.Add(arg);
    using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}.");
    var output = await process.StandardOutput.ReadToEndAsync();
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0 && !allowFailure)
        throw new InvalidOperationException($"{file} exited {process.ExitCode}: {error.Trim()}");
    return output.Trim();
}

sealed record PromotionManifest(
    string Tool,
    string Profile,
    string BaseRef,
    string BaseSha,
    string SourceRef,
    string SourceSha,
    IReadOnlyList<string> Commits,
    string CandidateBranch,
    string CandidateSha,
    string TreeSha,
    string Worktree,
    bool BuildSkipped,
    bool DockerTested,
    bool NativeE2ETested);

static class Env
{
    public static readonly string Repo = FindRepo();

    public static void Require(string command)
    {
        var names = OperatingSystem.IsWindows() ? new[] { command, $"{command}.exe", $"{command}.cmd" } : new[] { command };
        if ((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Any(d => names.Any(n => File.Exists(Path.Combine(d, n)))))
            return;
        throw new InvalidOperationException($"Required command not found on PATH: {command}");
    }

    static string FindRepo()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "global.json")) && Directory.Exists(Path.Combine(d.FullName, "src", "Articulate.Web")))
                return d.FullName;
        throw new InvalidOperationException("Run from the Articulate repository.");
    }
}

sealed class Opts
{
    readonly Dictionary<string, string?> values;
    Opts(Dictionary<string, string?> values) => this.values = values;

    public static Opts Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            var key = args[i][2..];
            values[key] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : null;
        }
        return new Opts(values);
    }

    public Opts Validate(string command, params string[] allowed)
    {
        var unknown = values.Keys.Where(key => !allowed.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (unknown.Length > 0) throw new ArgumentException($"Unknown option(s) for {command}: {string.Join(", ", unknown.Select(x => $"--{x}"))}.");
        foreach (var key in new[] { "profile", "base", "source", "commits" })
            if (values.ContainsKey(key) && string.IsNullOrWhiteSpace(values[key])) throw new ArgumentException($"--{key} requires a value.");
        foreach (var key in new[] { "skip-build", "docker" })
            if (values.ContainsKey(key) && values[key] is not null) throw new ArgumentException($"--{key} is a flag and does not accept a value.");
        if (Flag("skip-build") && Flag("docker"))
            throw new ArgumentException("--docker cannot be combined with --skip-build; Docker validation requires a built candidate.");
        return this;
    }

    public string Profile() => Required("profile").ToLowerInvariant() switch
    {
        "v17-lts" => "v17-lts",
        "v18-sts" => "v18-sts",
        var value => throw new ArgumentException($"--profile must be v17-lts or v18-sts (got '{value}').")
    };

    public string Required(string key) => String(key) ?? throw new ArgumentException($"--{key} is required.");

    public IReadOnlyList<string> Commits()
    {
        var commits = Required("commits")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
        if (commits.Length == 0) throw new ArgumentException("--commits requires at least one commit SHA.");
        return commits;
    }

    public string? String(string key) => values.TryGetValue(key, out var value) ? value : null;
    public bool Flag(string key) => values.TryGetValue(key, out var value) && value is null;
}
