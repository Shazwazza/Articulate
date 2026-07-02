#!/usr/bin/dotnet run

# nullable enable

// Articulate build utility. CLI reference lives in build/help.md.
//
// Conventions:
//   - Restore + build + test + pack + Docker matrix, one lane at a time.
//   - Shared src/*/bin and obj means lanes build sequentially with -m:1.
//   - Packages land in build/<Configuration>/<lane>/.

using System.Diagnostics;
using System.Net;

if (args.Length == 0 || args[0] is "-h" or "--help") { Help(null); return 0; }
if (args[0] == "help") { Help(args.Length > 1 ? args[1] : null); return 0; }

var command = args[0];
var opts = Opts.Parse(args[1..]);
try
{
    return command switch
    {
        "build"         => await BuildAsync(opts),
        "client"        => await ClientAsync(opts),
        "site"          => await SiteAsync(opts),
        "docker-build"  => await DockerAsync("build",  opts),
        "docker-dev"    => await DockerAsync("dev",    opts),
        "docker-prod"   => await DockerAsync("prod",   opts),
        "docker-status" => await DockerAsync("status", opts),
        "docker-test"   => await DockerTest(opts.String("lane", "all") ?? "all", opts.Flag("keep"), opts.Flag("skip-smoke")),
        "docker-ca"     => await DockerCaAsync(),
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
    if (i < 0) throw new ArgumentException($"No help topic '{topic}'.");
    Console.WriteLine(text[i..].TrimStart('\n'));
}

// ---- Commands ----

async Task<int> BuildAsync(Opts o)
{
    var sw = Stopwatch.StartNew();
    var lane = o.Lane();
    var cfg = o.String("configuration", Env.Get("BUILD_CONFIGURATION", "Release"));
    var inCi = Env.IsTrue(Env.CallerCi) || Env.IsTrue(Env.CallerGithubActions);
    var tests = o.Flag("tests") || o.Bool("tests", inCi);
    var client = o.Bool("client", inCi || cfg == "Release");
    var sample = o.Flag("sample") || o.Bool("sample", !inCi);
    var clean = o.Flag("clean");
    var releaseDir = Path.Combine(Env.Repo, "build", cfg ?? "Release", lane);
    Directory.CreateDirectory(releaseDir);

    var clientRoot = Path.Combine(Env.Repo, "src", "Articulate.Web", "Client");
    if (clean)
    {
        await Run("pnpm", new[] { "--workspace-concurrency=1", "-r", "run", "clean" }, cwd: clientRoot);
        DeleteDir(Path.Combine(clientRoot, "node_modules"));
    }
    await Run("pnpm",
        inCi ? new[] { "install", "--frozen-lockfile", "--prefer-offline" }
             : new[] { "install", "--prefer-offline" },
        cwd: clientRoot);

    var props = new List<string>
    {
        "-p:EnableClientBuild=" + client.ToString().ToLowerInvariant(),
        "-p:ArticulatePackageLane=" + lane,
        "-p:UmbracoClientVersion=" + (lane == "v18" ? "18" : "17"),
    };
    var packageVersion = Env.Get("ARTICULATE_PACKAGE_VERSION");
    if (lane == "v18" && string.IsNullOrWhiteSpace(packageVersion))
        packageVersion = File.ReadAllText(Path.Combine(Env.Repo, "build", "v18-version.txt")).Trim();
    if (packageVersion is not null) props.Add("-p:ArticulatePackageVersion=" + packageVersion);

    if (clean) DeleteBuildOutputs(Path.Combine(Env.Repo, "src"));

    var cfgName = cfg ?? "Release";
    var common = new[] { "-c", cfgName, "-m:1", "-p:BuildInParallel=false" };

    var restoreArgs = new[] { "restore", Env.Solution, "-v", "minimal", "-p:RestoreUseStaticGraphEvaluation=true" }
        .Concat(inCi ? new string[] { "--locked-mode" } : Array.Empty<string>())
        .Concat(props).ToArray();
    await Run("dotnet", restoreArgs);
    await Run("dotnet", new[] { "build", Env.Solution, "--no-restore", "-v", "minimal",
        "-p:UseSharedCompilation=false" }
        .Concat(common).Concat(props).ToArray());
    if (tests)
        await Run("dotnet", new[] { "test", Env.Solution, "--no-restore", "--no-build",
            "-v", "minimal" }
            .Concat(common).Concat(props).ToArray());

    var projects = new List<string> { Path.Combine(Env.Repo, "src", "Articulate.Web", "Articulate.Web.csproj") };
    if (sample) projects.Add(Path.Combine(Env.Repo, "src", "Articulate.Theme.Sample", "Articulate.Theme.Sample.csproj"));
    foreach (var project in projects)
        await Run("dotnet", new[] { "pack", project, "--no-restore", "--no-build",
            "-o", releaseDir, "-v", "minimal" }
            .Concat(common).Concat(props).ToArray());

    Console.WriteLine($"Completed in {sw.Elapsed.TotalSeconds:N1}s: {releaseDir}");
    return 0;
}

async Task<int> ClientAsync(Opts o)
{
    var workspace = Path.Combine(Env.Repo, "src", "Articulate.Web", "Client");
    await Run("pnpm", new[] { "install", "--frozen-lockfile" }, cwd: workspace);
    await Run("pnpm", new[] { "run", "check" }, cwd: workspace);
    await Run("pnpm", new[] { "run", "build" }, cwd: workspace);
    await Run("pnpm", new[] { "run", "lint" }, cwd: workspace);
    Console.WriteLine($"Client {o.Lane()} OK.");
    return 0;
}

async Task<int> SiteAsync(Opts o)
{
    var lane = o.Lane();
    var cfg = o.String("configuration", "Debug");
    var project = Path.Combine(Env.Repo, "src", "Articulate.Tests.Website", "Articulate.Tests.Website.csproj");
    if (o.Flag("reset")) DeleteDir(Path.Combine(Env.Repo, "src", "Articulate.Tests.Website", "umbraco"));
    await Run("dotnet", new[] { "run", "-c", cfg ?? "Debug", "--project", project, $"-p:ArticulatePackageLane={lane}" }, cwd: Env.Repo);
    return 0;
}

async Task<int> DockerAsync(string sub, Opts o)
{
    var lane = o.Lane();
    Env.Require("docker");
    Env.RequireSecret();
    ConfigureLane(lane);
    await EnsurePackages(lane);

    return sub switch
    {
        "build"  => await DockerBuild(lane, o.String("tag", $"articulate-local:{lane}") ?? $"articulate-local:{lane}"),
        "dev"    => await DockerDev(lane, o.Flag("reset"), o.Flag("skip-smoke")),
        "prod"   => await DockerProd(lane),
        "status" => await DockerStatus(),
        _ => throw new ArgumentException($"Unknown docker subcommand '{sub}'.")
    };
}

async Task<int> DockerBuild(string lane, string tag)
{
    var cms = lane == "v18" ? "[18.0.0-*,19.0.0)" : "[17.4.0,18.0.0)";
    await Run("docker", new[]
    {
        "build", "--file", "Dockerfile", "--target", "chiseled", "--tag", tag,
        "--build-arg", $"PACKAGE_SOURCE=build/Release/{lane}",
        "--build-arg", $"UMBRACO_CMS_VERSION={cms}",
        "."
    }, cwd: Env.Repo);
    return 0;
}

async Task<int> DockerDev(string lane, bool reset, bool skipSmoke)
{
    Environment.SetEnvironmentVariable("UMBRACO_RUNTIME_MODE", "BackofficeDevelopment");
    ConfigureLane(lane);
    if (reset) await Compose(new[] { "down", "-v" }, allowFailure: true);
    await Compose(new[] { "up", "-d", "--build" });
    var url = Env.Get("UMBRACO_PUBLIC_URL") ?? $"https://localhost:{Env.Get("CADDY_HTTPS_PORT", "44317")}/";
    await WaitFor(new Uri(new Uri(url), "umbraco/"));
    if (!skipSmoke)
    {
        Env.RequireSecret();
        await Smoke("publish");
        await Smoke("confirm");
    }
    return 0;
}

async Task<int> DockerProd(string lane)
{
    Env.RequireSecret();
    ConfigureLane(lane);
    Environment.SetEnvironmentVariable("UMBRACO_RUNTIME_MODE", "Production");
    await Compose(new[] { "up", "-d", "--force-recreate" });
    var url = Env.Get("UMBRACO_PUBLIC_URL") ?? $"https://localhost:{Env.Get("CADDY_HTTPS_PORT", "44317")}/";
    await WaitFor(new Uri(new Uri(url), "umbraco/"));
    await Smoke("smoke");
    await Smoke("theme");
    return 0;
}

async Task<int> DockerStatus()
{
    await Compose(new[] { "ps" });
    var id = (await Capture("docker", new[] { "compose", "ps", "-q", "articulate" }, Env.Repo)).Trim();
    if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("articulate container is not running.");

    var dir = Path.Combine(Path.GetTempPath(), $"art-status-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);
    try
    {
        await Run("docker", new[] { "cp", $"{id}:/app/wwwroot/App_Plugins/Articulate/.", dir }, cwd: Env.Repo);
        var bundle = Directory.EnumerateFiles(dir, "articulate-backoffice.js", SearchOption.AllDirectories).FirstOrDefault();
        var manifest = Directory.EnumerateFiles(dir, "umbraco-package.json", SearchOption.AllDirectories).FirstOrDefault();
        if (bundle is null || manifest is null)
            throw new InvalidOperationException("Container is missing Backoffice bundle or umbraco-package.json.");
        Console.WriteLine($"Backoffice bundle: {Path.GetRelativePath(dir, bundle)}");
        Console.WriteLine($"Package manifest:  {Path.GetRelativePath(dir, manifest)}");
    }
    finally { DeleteDir(dir); }
    return 0;
}

async Task<int> DockerTest(string lane, bool keep, bool skipSmoke)
{
    if (lane is not ("v17" or "v18" or "all"))
        throw new ArgumentException("--lane must be v17, v18, or all.");
    var lanes = lane is "all" ? new[] { "v17", "v18" } : new[] { lane };
    foreach (var l in lanes)
    {
        ConfigureLane(l);
        await EnsurePackages(l);
        await Compose(new[] { "build", "--no-cache", "--pull" });
        await DockerDev(l, reset: false, skipSmoke);
        if (!skipSmoke) await DockerProd(l);
        Console.WriteLine($"PASSED: {l}");
        if (!keep) await Compose(new[] { "down", "-v" }, allowFailure: true);
    }
    return 0;
}

// ---- Lane defaults ----

void ConfigureLane(string lane)
{
    var https = lane == "v18" ? "44318" : "44317";
    var http = lane == "v18" ? "44381" : "44380";
    foreach (var (k, vf) in Env.LaneDefaults)
        Env.SetDefault(k, vf(lane, https, http));
    var host = Env.Get("CADDY_HTTPS_HOST", $"localhost:{https}") ?? $"localhost:{https}";
    var colon = host.LastIndexOf(':');
    Env.SetDefault("CADDY_TLS_HOST", colon >= 0 ? host[..colon] : host);
}

async Task EnsurePackages(string lane)
{
    var major = lane == "v18" ? "7" : "6";
    var dir = Path.Combine(Env.Repo, "build", "Release", lane);
    var ok = Directory.Exists(dir)
        && Directory.EnumerateFiles(dir, $"Articulate.{major}.*.nupkg").Any()
        && Directory.EnumerateFiles(dir, $"Articulate.Theme.Sample.{major}.*.nupkg").Any();
    if (!ok)
    {
        Env.SetDefault("ARTICULATE_PACKAGE_LANE", lane);
        await BuildAsync(Opts.Of(("lane", lane), ("sample", "true")));
    }
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
    var psi = new ProcessStartInfo(file) { UseShellExecute = false, WorkingDirectory = cwd, RedirectStandardOutput = true };
    foreach (var a in args) psi.ArgumentList.Add(a);
    using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}.");
    var output = await p.StandardOutput.ReadToEndAsync();
    await p.WaitForExitAsync();
    if (p.ExitCode != 0) throw new InvalidOperationException($"{file} exited {p.ExitCode}.");
    return output;
}

Task Compose(IEnumerable<string> args, bool allowFailure = false)
    => Run("docker", new[] { "compose" }.Concat(args), cwd: Env.Repo, allowFailure: allowFailure);

Task Smoke(string command)
    => Run(Env.Get("NODE_BIN", "node") ?? "node",
        new[] { Path.Combine(Env.DockerDir, "smoke.mjs"), command },
        cwd: Env.Repo);

async Task WaitFor(Uri uri)
{
    using var h = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
    using var c = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(10) };
    var deadline = DateTime.UtcNow.AddMinutes(5);
    while (DateTime.UtcNow < deadline)
    {
        try
        {
            using var r = await c.GetAsync(uri);
            if (r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Found) return;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        await Task.Delay(2000);
    }
    throw new TimeoutException($"Timed out waiting for {uri}.");
}

void DeleteDir(string path) { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }

void DeleteBuildOutputs(string root)
{
    foreach (var d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
        .Where(p => Path.GetFileName(p) is "bin" or "obj").OrderByDescending(p => p.Length))
        DeleteDir(d);
}

// ---- CA trust ----

async Task<int> DockerCaAsync()
{
    var script = OperatingSystem.IsWindows() ? "Trust-CaddyRootCA.ps1" : "trust-caddy-root-ca.sh";
    var path = Path.Combine(Env.DockerDir, script);
    var args = OperatingSystem.IsWindows()
        ? new[] { "-ExecutionPolicy", "Bypass", "-File", path }
        : new[] { path };
    await Run(OperatingSystem.IsWindows() ? "pwsh" : "sh", args, Env.Repo);
    return 0;
}

// ---- Env: paths, env vars, defaults ----

static class Env
{
    public static readonly string Repo = FindRepo();
    public static readonly string Solution = Path.Combine(Repo, "src", "Articulate.sln");
    public static readonly string DockerDir = Path.Combine(Repo, "build", "docker-site");

    // Capture once at process start; DockerTest re-enters via SetEnvironmentVariable
    // which would otherwise overwrite the parent's value mid-run.
    public static readonly string? CallerCi = Environment.GetEnvironmentVariable("CI");
    public static readonly string? CallerGithubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");

    public static string? Get(string name, string? fallback = null)
        => Environment.GetEnvironmentVariable(name) ?? fallback;

    public static bool IsTrue(string? v) => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

    public static void SetDefault(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            Environment.SetEnvironmentVariable(name, value);
    }

    public static void RequireSecret()
    {
        SetDefault("ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET", "articulate-dev-local-secret");
        SetDefault("ARTICULATE_DEV_AUTOMATION_CLIENT_ID", "articulate-dev-automation");
    }

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

    // Per-lane env defaults. (Key, ValueFn(lane, httpsPort, httpPort)) — the helpers
    // take the lane and resolved ports so the closure doesn't repeat the v18 logic.
    public static readonly (string Key, Func<string, string, string, string> Value)[] LaneDefaults =
    {
        ("ARTICULATE_PACKAGE_LANE",                              (l, _, _) => l),
        ("COMPOSE_PROJECT_NAME",                                 (l, _, _) => $"art_{l}"),
        ("COMPOSE_VOLUME_PREFIX",                                (l, _, _) => $"art_{l}"),
        ("IMAGE_TAG",                                            (l, _, _) => $"articulate-local:{l}"),
        ("PACKAGE_SOURCE",                                       (l, _, _) => $"build/Release/{l}"),
        ("UMBRACO_CMS_VERSION",                                  (l, _, _) => l == "v18" ? "[18.0.0-*,19.0.0)" : "[17.4.0,18.0.0)"),
        ("Umbraco__CMS__Security__AuthCookieName",               (l, _, _) => $"UMB_UCONTEXT-{l}"),
        ("Umbraco__CMS__Security__BackOfficeTokenCookie__SiteName", (l, _, _) => $"-{l}"),
        ("CADDY_HTTPS_PORT",                                     (_, h, _) => h),
        ("CADDY_HTTP_PORT",                                      (_, _, p) => p),
        ("CADDY_HTTPS_HOST",                                     (_, h, _) => $"localhost:{h}"),
        ("UMBRACO_PUBLIC_HOST",                                  (_, h, _) => $"https://localhost:{h}"),
        ("UMBRACO_PUBLIC_URL",                                   (_, h, _) => $"https://localhost:{h}/"),
        ("ARTICULATE_REDIRECT_URI",                              (_, h, _) => $"https://localhost:{h}/a-new/"),
        ("ARTICULATE_LOGOUT_REDIRECT_URI",                       (_, h, _) => $"https://localhost:{h}/"),
    };

    static string FindRepo()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(d.FullName, "src", "Articulate.Web")))
                return d.FullName;
        throw new InvalidOperationException("Run from the Articulate repository.");
    }
}

// ---- Options: dict + 4 accessors ----

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

    public static Opts Of(params (string Key, string? Value)[] kvs)
    {
        var d = new Dictionary<string, string?>();
        foreach (var (k, v) in kvs) d[k] = v;
        return new Opts(d);
    }

    public string Lane() => (String("lane") ?? Env.Get("ARTICULATE_PACKAGE_LANE", "v17") ?? "v17").ToLowerInvariant() switch
    {
        "v17" => "v17",
        "v18" => "v18",
        var x => throw new ArgumentException($"--lane must be v17 or v18 (got '{x}').")
    };

    public string? String(string key, string? fallback = null) => _v.TryGetValue(key, out var v) ? v : fallback;

    public bool Bool(string key, bool fallback = false)
        => _v.TryGetValue(key, out var v) && v is not null
            ? bool.TryParse(v, out var b) ? b : Env.IsTrue(v)
            : fallback;

    public bool Flag(string key) => _v.TryGetValue(key, out var v) && v is null;
}