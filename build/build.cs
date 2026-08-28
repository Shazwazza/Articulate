#!/usr/bin/dotnet run
#:property NoWarn=SA1400,SA1503,SA1519,SA1116,SA1117,SA1122,SA1649,IDE0008,IDE0011,IDE0040,SA1500

# nullable enable

// Articulate build utility. CLI reference lives in build/help.md.
//
// Conventions:
//   - Restore + build + test + pack, one lane at a time.
//   - Shared src/*/bin and obj means lanes build sequentially with -m:1.
//   - Use --clean when switching package lanes in the same checkout.
//   - Packages land in build/<Configuration>/<lane>/.

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

try
{
    if (args.Length == 0 || args[0] is "-h" or "--help") { Help(null); return 0; }
    if (args[0] == "help") { Help(args.Length > 1 ? args[1] : null); return 0; }

    var command = args[0];
    var opts = Opts.Parse(args[1..]);
    return command switch
    {
        "build"         => await BuildAsync(opts.Validate(command, "lane", "configuration", "tests", "client", "sample", "clean")),
        "client"        => await ClientAsync(opts.Validate(command, "lane")),
        "site"          => await SiteAsync(opts.Validate(command, "lane", "configuration", "reset")),
        _ => throw new ArgumentException($"Unknown command '{command}'. Run with --help.")
    };
}
catch (Exception e) { Console.Error.WriteLine($"ERROR: {e.Message}"); return 1; }

// ---- Help ----

void Help(string? topic)
{
    var path = Path.Combine(Env.Repo, "build", "help.md");
    var text = File.ReadAllText(path);
    if (topic is null) { Console.WriteLine(text); return; }
    var marker = $"### {topic}";
    var i = text.IndexOf(marker, StringComparison.Ordinal);
    while (i >= 0 &&
           ((i > 0 && text[i - 1] != '\n') ||
            (i + marker.Length < text.Length && text[i + marker.Length] is not ('\r' or '\n'))))
        i = text.IndexOf(marker, i + marker.Length, StringComparison.Ordinal);
    if (i < 0) throw new ArgumentException($"No help topic '{topic}'.");
    var searchFrom = i + marker.Length;
    var nextH2 = text.IndexOf("\n## ", searchFrom, StringComparison.Ordinal);
    var nextH3 = text.IndexOf("\n### ", searchFrom, StringComparison.Ordinal);
    int end = text.Length;
    if (nextH2 >= 0 && nextH3 >= 0) end = Math.Min(nextH2, nextH3);
    else if (nextH2 >= 0) end = nextH2;
    else if (nextH3 >= 0) end = nextH3;
    Console.WriteLine(text[i..end].TrimStart('\n'));
}

// ---- Commands ----

async Task<int> BuildAsync(Opts o)
{
    var sw = Stopwatch.StartNew();
    var lane = o.Lane();
    var cfg = o.String("configuration", Env.Get("BUILD_CONFIGURATION", "Release"));
    ValidateConfiguration(cfg);
    var inCi = Env.IsTrue(Env.CallerCi) || Env.IsTrue(Env.CallerGithubActions) || Env.IsTrue(Env.CallerAct);
    if (inCi) Environment.SetEnvironmentVariable("CI", "true");
    var defaults = BuildDefaults.Resolve(o, inCi, cfg);
    var clean = o.Flag("clean");
    var releaseDir = Path.Combine(Env.Repo, "build", cfg ?? "Release", lane);
    Directory.CreateDirectory(releaseDir);

    var clientAssetsDir = Path.Combine(Env.Repo, "build", "ClientAssets");
    var backofficeDir = Path.Combine(Env.Repo, "src", "Articulate.Web", "wwwroot", "App_Plugins", "Articulate", "BackOffice");
    var activeLanePath = Path.Combine(clientAssetsDir, "active-lane.txt");
    var activeVersionPath = Path.Combine(clientAssetsDir, "active-version.txt");

    var props = new List<string>
    {
        "-p:EnableClientBuild=" + defaults.Client.ToString().ToLowerInvariant(),
        "-p:ArticulatePackageLane=" + lane,
    };
    var packageVersion = await ResolvePackageVersion(lane);
    props.Add("-p:ArticulatePackageVersion=" + packageVersion);

    Console.WriteLine($"Build: {lane}, {cfg}, package {packageVersion}{(clean ? " (Clean Build)" : "")}");
    DeleteExistingPackages(releaseDir);

    if (clean)
    {
        await Run("dotnet", new[] { "build-server", "shutdown" }, cwd: Env.Repo, allowFailure: true);
        DeleteBuildOutputs(Path.Combine(Env.Repo, "src"));
        DeleteDir(clientAssetsDir);
    }

    if (defaults.Client)
    {
        var activeLane = File.Exists(activeLanePath) ? File.ReadAllText(activeLanePath).Trim() : null;
        var activeVersion = File.Exists(activeVersionPath) ? File.ReadAllText(activeVersionPath).Trim() : null;
        if (!clean && activeLane is not null && !string.Equals(activeLane, lane, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Client assets belong to {activeLane}; rerun with --clean before building {lane}.");
        if (clean || activeLane is null || !string.Equals(activeVersion, packageVersion, StringComparison.Ordinal))
        {
            DeleteDir(backofficeDir);
            DeleteClientBuildStamps(clientAssetsDir, lane);
        }
        DeleteFile(activeLanePath);
        DeleteFile(activeVersionPath);
    }
    else
    {
        DeleteDir(backofficeDir);
        DeleteClientBuildStamps(clientAssetsDir, lane);
        DeleteFile(activeLanePath);
        DeleteFile(activeVersionPath);
    }

    var cfgName = cfg ?? "Release";
    var common = new[] { "-c", cfgName, "-m:1", "-p:BuildInParallel=false" };

    var restoreArgs = new[] { "restore", Env.Solution, "-v", "minimal", "-p:RestoreUseStaticGraphEvaluation=true" }
        .Concat(inCi ? new string[] { "--locked-mode" } : Array.Empty<string>())
        .Concat(props).ToArray();
    await Run("dotnet", restoreArgs);
    await Run("dotnet", new[] { "build", Env.Solution, "--no-restore", "-v", "minimal",
        "-p:UseSharedCompilation=false" }
        .Concat(common).Concat(props).ToArray());
    if (defaults.Tests)
        await Run("dotnet", new[] { "test", Env.Solution, "--no-restore", "--no-build",
            "-v", "minimal" }
            .Concat(common).Concat(props).ToArray());

    var projects = new List<string> { Path.Combine(Env.Repo, "src", "Articulate.Web", "Articulate.Web.csproj") };
    if (defaults.Sample) projects.Add(Path.Combine(Env.Repo, "src", "Articulate.Theme.Sample", "Articulate.Theme.Sample.csproj"));
    foreach (var project in projects)
        await Run("dotnet", new[] { "pack", project, "--no-restore", "--no-build",
            "-o", releaseDir, "-v", "minimal" }
            .Concat(common).Concat(props).ToArray());

    VerifyPackages(releaseDir, packageVersion, defaults.Sample);
    if (defaults.Client)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(activeLanePath)!);
        File.WriteAllText(activeLanePath, lane + Environment.NewLine);
        File.WriteAllText(activeVersionPath, packageVersion + Environment.NewLine);
    }

    Console.WriteLine($"Completed in {sw.Elapsed.TotalSeconds:N1}s: {releaseDir}");
    return 0;
}

async Task<int> ClientAsync(Opts o)
{
    var workspace = Path.Combine(Env.Repo, "src", "Articulate.Web", "Client");
    var laneDir = Path.Combine(workspace, o.Lane());
    DeleteDir(Path.Combine(Env.Repo, "build", "ClientAssets"));
    var requiredNodeVersion = File.ReadAllText(Path.Combine(workspace, ".node-version")).Trim();
    var requiredNodeMajor = requiredNodeVersion.Split('.')[0];
    if (string.IsNullOrWhiteSpace(requiredNodeMajor))
        throw new InvalidOperationException("Client .node-version is empty.");
    var nodeVersion = (await Capture("node", new[] { "--version" }, workspace)).Trim();
    var nodeMajor = nodeVersion.TrimStart('v').Split('.')[0];
    if (!string.Equals(nodeMajor, requiredNodeMajor, StringComparison.Ordinal))
        throw new InvalidOperationException($"Client build requires Node {requiredNodeVersion} (found {nodeVersion}). See .node-version or CI setup.");
    using var packageJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace, "package.json")));
    var packageManager = packageJson.RootElement.GetProperty("packageManager").GetString()
        ?? throw new InvalidOperationException("Client package.json has no packageManager.");
    const string pnpmPrefix = "pnpm@";
    if (!packageManager.StartsWith(pnpmPrefix, StringComparison.Ordinal))
        throw new InvalidOperationException($"Client packageManager must use pnpm (found {packageManager}).");
    var requiredPnpmVersion = packageManager[pnpmPrefix.Length..];
    var pnpmVersion = (await Capture("pnpm", new[] { "--version" }, workspace)).Trim();
    if (pnpmVersion != requiredPnpmVersion)
        throw new InvalidOperationException($"Client build requires pnpm {requiredPnpmVersion} (found {pnpmVersion}).");
    await Run("pnpm", new[] { "install", "--frozen-lockfile" }, cwd: workspace);
    await Run("pnpm", new[] { "run", "check" }, cwd: laneDir);
    await Run("pnpm", new[] { "run", "build" }, cwd: laneDir);
    await Run("pnpm", new[] { "run", "lint" }, cwd: laneDir);
    Console.WriteLine($"Client {o.Lane()} OK.");
    return 0;
}

async Task<int> SiteAsync(Opts o)
{
    var lane = o.Lane();
    var cfg = o.String("configuration", "Debug");
    ValidateConfiguration(cfg);
    var project = Path.Combine(Env.Repo, "src", "Articulate.Tests.Website", "Articulate.Tests.Website.csproj");
    if (o.Flag("reset")) DeleteDir(Path.Combine(Env.Repo, "src", "Articulate.Tests.Website", "umbraco"));
    await Run("dotnet", new[] { "run", "-c", cfg ?? "Debug", "--project", project, $"-p:ArticulatePackageLane={lane}" }, cwd: Env.Repo);
    return 0;
}

async Task<string> ResolvePackageVersion(string lane)
{
    var packageVersion = Env.Get("ARTICULATE_PACKAGE_VERSION");
    if (!string.IsNullOrWhiteSpace(packageVersion)) return packageVersion;

    var inCi = Env.IsTrue(Env.CallerCi) || Env.IsTrue(Env.CallerGithubActions) || Env.IsTrue(Env.CallerAct);
    var nbgv = inCi ? Env.Get("NBGV_SemVer2") : null;
    if (string.IsNullOrWhiteSpace(nbgv))
        nbgv = (await Capture("nbgv", new[] { "get-version", "-v", "SemVer2" }, Env.Repo)).Trim();
    if (lane == "v17") return nbgv;

    var baseVersionPath = Path.Combine(Env.Repo, $"version-{lane}.txt");
    var baseVersion = File.ReadAllText(baseVersionPath).Trim();
    if (string.IsNullOrWhiteSpace(baseVersion))
        throw new InvalidOperationException($"{Path.GetFileName(baseVersionPath)} is empty.");

    var commitMatch = Regex.Match(nbgv, @"[-.]g[a-f0-9]+$");
    return baseVersion + commitMatch.Value;
}

void ValidateConfiguration(string? configuration)
{
    if (configuration is not ("Debug" or "Release"))
        throw new ArgumentException("--configuration must be Debug or Release.");
}

// ---- Shell helpers ----

async Task Run(string file, IEnumerable<string> args, string? cwd = null, bool allowFailure = false)
{
    Env.Require(file);
    var psi = new ProcessStartInfo(file) { UseShellExecute = false };
    if (cwd is not null) psi.WorkingDirectory = cwd;
    foreach (var a in args) psi.ArgumentList.Add(a);
    Console.WriteLine($"> {file} {string.Join(' ', args)}");
    using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}.");
    await p.WaitForExitAsync();
    if (p.ExitCode != 0 && !allowFailure)
        throw new InvalidOperationException($"{file} exited {p.ExitCode}.");
}

async Task<string> Capture(string file, IEnumerable<string> args, string cwd)
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
    if (process.ExitCode != 0) throw new InvalidOperationException($"{file} exited {process.ExitCode}: {error.Trim()}");
    return output;
}

void DeleteDir(string path) { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
void DeleteClientBuildStamps(string clientAssetsDir, string lane)
{
    if (!Directory.Exists(clientAssetsDir)) return;
    var laneSuffix = lane[1..];
    foreach (var stamp in Directory.EnumerateFiles(clientAssetsDir, $"BackofficeClient_v{laneSuffix}*.stamp"))
        DeleteFile(stamp);
}

void DeleteBuildOutputs(string root)
{
    foreach (var d in EnumerateSourceDirectories(root)
        .Where(p => Path.GetFileName(p) is "bin" or "obj").OrderByDescending(p => p.Length))
        DeleteDir(d);
}

IEnumerable<string> EnumerateSourceDirectories(string root)
{
    foreach (var directory in Directory.EnumerateDirectories(root))
    {
        var name = Path.GetFileName(directory);
        if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
        yield return directory;
        if (name is not ("bin" or "obj"))
            foreach (var nested in EnumerateSourceDirectories(directory)) yield return nested;
    }
}

void DeleteExistingPackages(string releaseDir)
{
    if (!Directory.Exists(releaseDir)) return;
    foreach (var file in Directory.EnumerateFiles(releaseDir))
    {
        var name = Path.GetFileName(file);
        if (Regex.IsMatch(name, @"^Articulate(?:\.Theme\.Sample)?\..*\.s?nupkg$"))
            File.Delete(file);
    }
}

void VerifyPackages(string releaseDir, string version, bool sample)
{
    var expected = new List<string>
    {
        $"Articulate.{version}.nupkg",
        $"Articulate.{version}.snupkg",
    };
    if (sample) expected.Add($"Articulate.Theme.Sample.{version}.nupkg");

    var missing = expected
        .Where(file => !File.Exists(Path.Combine(releaseDir, file)))
        .ToArray();
    if (missing.Length > 0)
        throw new InvalidOperationException($"Package output is incomplete in '{releaseDir}': missing {string.Join(", ", missing)}.");
}

// ---- Env: paths, env vars, defaults ----

static class Env
{
    public static readonly string Repo = FindRepo();
    public static readonly string Solution = Path.Combine(Repo, "src", "Articulate.sln");
    public static readonly string? CallerCi = Environment.GetEnvironmentVariable("CI");
    public static readonly string? CallerGithubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
    public static readonly string? CallerAct = Environment.GetEnvironmentVariable("ACT");

    public static string? Get(string name, string? fallback = null)
        => Environment.GetEnvironmentVariable(name) ?? fallback;

    public static bool IsTrue(string? v) => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

    public static void Require(string command)
    {
        if (Path.IsPathRooted(command) && File.Exists(command)) return;
        var names = OperatingSystem.IsWindows()
            ? new[] { command, $"{command}.exe", $"{command}.cmd" }
            : new[] { command };
        if ((Get("PATH") ?? "").Split(Path.PathSeparator).Any(d => names.Any(n => File.Exists(Path.Combine(d, n)))))
            return;
        throw new InvalidOperationException($"Required command not found on PATH: {command}");
    }

    static string FindRepo()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(d.FullName, "src", "Articulate.Web")))
                return d.FullName;
        throw new InvalidOperationException("Run from the Articulate repository.");
    }
}

// ---- Options ----

// ---- Build defaults ----

record BuildDefaults(bool Tests, bool Client, bool Sample)
{
    public static BuildDefaults Resolve(Opts o, bool inCi, string? cfg) => new(
        Tests:  o.Flag("tests")  || o.Bool("tests",  "RUN_TESTS",         inCi),
        Client: o.Bool("client", "ENABLE_CLIENT_BUILD",                  inCi || cfg == "Release"),
        Sample: o.Flag("sample") || o.Bool("sample", "PACK_SAMPLE_THEME", !inCi));
}

sealed class Opts
{
    readonly Dictionary<string, string?> _v = new(StringComparer.OrdinalIgnoreCase);
    Opts(Dictionary<string, string?> v) { this._v = v; }

    public static Opts Parse(string[] a)
    {
        var d = new Dictionary<string, string?>();
        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].StartsWith("--")) throw new ArgumentException($"Unexpected argument '{a[i]}'.");
            var key = a[i][2..];
            d[key] = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[++i] : null;
        }
        return new Opts(d);
    }

    public Opts Validate(string command, params string[] allowed)
    {
        var unknown = _v.Keys.Where(key => !allowed.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (unknown.Length > 0)
            throw new ArgumentException(
                $"Unknown option(s) for {command}: {string.Join(", ", unknown.Select(x => $"--{x}"))}. " +
                $"Run 'dotnet run build/build.cs -- help {command}'.");

        RequireValue("lane");
        RequireValue("configuration");
        RequireBoolean("tests");
        RequireBoolean("client");
        RequireBoolean("sample");
        RequireFlag("clean");
        RequireFlag("reset");

        if (String("configuration") is { } configuration &&
            configuration is not ("Debug" or "Release"))
            throw new ArgumentException("--configuration must be Debug or Release.");

        return this;
    }

    public string Lane() => (String("lane") ?? Env.Get("ARTICULATE_PACKAGE_LANE", "v17") ?? "v17").ToLowerInvariant() switch
    {
        "v17" => "v17",
        "v18" => "v18",
        var x => throw new ArgumentException($"--lane must be v17 or v18 (got '{x}').")
    };

    public string? String(string key, string? fallback = null) => _v.TryGetValue(key, out var v) ? v : fallback;

    public bool Bool(string key, string env, bool fallback)
        => _v.TryGetValue(key, out var v) && v is not null
            ? bool.TryParse(v, out var b) ? b : Env.IsTrue(v)
            : Env.Get(env) is { } envValue ? Env.IsTrue(envValue) : fallback;

    public bool Flag(string key) => _v.TryGetValue(key, out var v) && v is null;

    void RequireValue(string key)
    {
        if (_v.ContainsKey(key) && string.IsNullOrWhiteSpace(_v[key]))
            throw new ArgumentException($"--{key} requires a value.");
    }

    void RequireBoolean(string key)
    {
        if (String(key) is { } value && !bool.TryParse(value, out _))
            throw new ArgumentException($"--{key} must be true or false.");
    }

    void RequireFlag(string key)
    {
        if (String(key) is not null)
            throw new ArgumentException($"--{key} is a flag and does not accept a value.");
    }
}
