#!/usr/bin/dotnet run
#:property NoWarn=SA1400,SA1503,SA1519,SA1116,SA1117,SA1122,SA1649,IDE0008,IDE0011,IDE0040,SA1500

# nullable enable

// Articulate build utility. CLI reference lives in build/help.md.
//
// Conventions:
//   - Restore + build + test + pack + Docker matrix, one lane at a time.
//   - Shared src/*/bin and obj means lanes build sequentially with -m:1.
//   - Packages land in build/<Configuration>/<lane>/.

using System.Diagnostics;
using System.Net;
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
        "docker-build"  => await DockerAsync("build",  opts.Validate(command, "lane", "tag")),
        "docker-dev"    => await DockerAsync("dev",    opts.Validate(command, "lane", "skip-smoke", "reset")),
        "docker-prod"   => await DockerAsync("prod",   opts.Validate(command, "lane", "skip-smoke")),
        "docker-status" => await DockerAsync("status", opts.Validate(command, "lane")),
        "docker-test"   => await DockerTest(opts.Validate(command, "lane", "keep", "skip-smoke")),
        "docker-ca"     => await DockerCaAsync(opts.Validate(command)),
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
    var inCi = Env.IsTrue(Env.CallerCi) || Env.IsTrue(Env.CallerGithubActions);
    var defaults = BuildDefaults.Resolve(o, inCi, cfg);
    var clean = o.Flag("clean");
    var releaseDir = Path.Combine(Env.Repo, "build", cfg ?? "Release", lane);
    Directory.CreateDirectory(releaseDir);

    var clientRoot = Path.Combine(Env.Repo, "src", "Articulate.Web", "Client");
    if (clean)
    {
        await Run("pnpm", new[] { "--workspace-concurrency=1", "-r", "run", "clean" }, cwd: clientRoot);
        DeleteDir(Path.Combine(clientRoot, "node_modules"));
    }

    var props = new List<string>
    {
        "-p:EnableClientBuild=" + defaults.Client.ToString().ToLowerInvariant(),
        "-p:ArticulatePackageLane=" + lane,
        "-p:UmbracoClientVersion=" + (lane == "v18" ? "18" : "17"),
    };
    var packageVersion = await ResolvePackageVersion(lane);
    if (packageVersion is not null) props.Add("-p:ArticulatePackageVersion=" + packageVersion);

    Console.WriteLine($"Build: {lane}, {cfg}, package {packageVersion ?? "NBGV"}{(clean ? " (Clean Build)" : "")}");
    Environment.SetEnvironmentVariable("CI", "true");
    DeleteExistingPackages(releaseDir, lane);

    if (clean)
    {
        await Run("dotnet", new[] { "build-server", "shutdown" }, cwd: Env.Repo, allowFailure: true);
        DeleteBuildOutputs(Path.Combine(Env.Repo, "src"));
        DeleteDir(Path.Combine(Env.Repo, "build", "ClientAssets"));
    }

    DeleteDir(Path.Combine(Env.Repo, "src", "Articulate.Web", "wwwroot", "App_Plugins", "Articulate", "BackOffice"));
    var clientVersion = lane == "v18" ? "18" : "17";
    var stamp = Path.Combine(Env.Repo, "build", "ClientAssets", $"BackofficeClient_v{clientVersion}.stamp");
    if (File.Exists(stamp)) File.Delete(stamp);

    if (defaults.Client)
    {
        await Run("pnpm",
            inCi ? new[] { "install", "--frozen-lockfile", "--prefer-offline" }
                 : new[] { "install", "--prefer-offline" },
            cwd: clientRoot);
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

    Console.WriteLine($"Completed in {sw.Elapsed.TotalSeconds:N1}s: {releaseDir}");
    return 0;
}

async Task<int> ClientAsync(Opts o)
{
    var workspace = Path.Combine(Env.Repo, "src", "Articulate.Web", "Client");
    var laneDir = Path.Combine(workspace, o.Lane());
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

async Task<int> DockerAsync(string sub, Opts o)
{
    var lane = o.Lane();
    Env.Require("docker");
    ConfigureLane(lane);
    if (sub is "build" or "dev" or "prod") await EnsurePackages(lane);

    return sub switch
    {
        "build"  => await DockerBuild(lane, o.String("tag", $"articulate-local:{lane}") ?? $"articulate-local:{lane}"),
        "dev"    => await DockerDev(lane, o.Flag("reset"), o.Flag("skip-smoke")),
        "prod"   => await DockerProd(lane, o.Flag("skip-smoke")),
        "status" => await DockerStatus(o),
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
        "--build-arg", $"USE_TINYMCE_UMBRACO={Env.Get("USE_TINYMCE_UMBRACO", "false")}",
        "--build-arg", $"TINYMCE_UMBRACO_PACKAGE_VERSION={Env.Get("TINYMCE_UMBRACO_PACKAGE_VERSION", "17.1.0")}",
        "--build-arg", $"TINYMCE_UMBRACO_PACKAGE_SOURCE={Env.Get("TINYMCE_UMBRACO_PACKAGE_SOURCE", "build/LocalPackages/TinyMCE.Umbraco")}",
        "--build-arg", $"USE_TINYMCE_UMBRACO_PACKAGE_SOURCE={Env.Get("USE_TINYMCE_UMBRACO_PACKAGE_SOURCE", "false")}",
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

async Task<int> DockerProd(string lane, bool skipSmoke)
{
    Env.RequireSecret();
    ConfigureLane(lane);
    Environment.SetEnvironmentVariable("UMBRACO_RUNTIME_MODE", "Production");
    await Compose(new[] { "up", "-d", "--force-recreate" });
    var url = Env.Get("UMBRACO_PUBLIC_URL") ?? $"https://localhost:{Env.Get("CADDY_HTTPS_PORT", "44317")}/";
    await WaitFor(new Uri(new Uri(url), "umbraco/"));
    if (!skipSmoke)
    {
        await Smoke("smoke");
        await Smoke("theme");
    }
    return 0;
}

async Task<int> DockerStatus(Opts o)
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

async Task<int> DockerTest(Opts o)
{
    var lane = o.String("lane", "all") ?? "all";
    var keep = o.Flag("keep");
    var skipSmoke = o.Flag("skip-smoke");
    if (lane is not ("v17" or "v18" or "all"))
        throw new ArgumentException("--lane must be v17, v18, or all.");
    var lanes = lane is "all" ? new[] { "v17", "v18" } : new[] { lane };
    foreach (var l in lanes)
    {
        ConfigureLane(l);
        await EnsurePackages(l);
        try
        {
            await Compose(new[] { "build", "--no-cache", "--pull" });
            await DockerDev(l, reset: false, skipSmoke);
            if (!skipSmoke) await DockerProd(l, skipSmoke: false);
            Console.WriteLine($"PASSED: {l}");
        }
        finally
        {
            if (!keep) await Compose(new[] { "down", "-v" }, allowFailure: true);
        }
    }
    return 0;
}

// ---- Lane defaults ----

void ConfigureLane(string lane)
{
    var https = lane == "v18" ? "44318" : "44317";
    var http = lane == "v18" ? "44381" : "44380";
    foreach (var (k, vf) in Env.LaneDefaults)
        Env.Set(k, vf(lane, https, http));
    var host = Env.HostOverride("CADDY_HTTPS_HOST") ?? $"localhost:{https}";
    var colon = host.LastIndexOf(':');
    Env.SetHostValue("CADDY_HTTPS_PORT", https);
    Env.SetHostValue("CADDY_HTTP_PORT", http);
    Env.SetHostValue("CADDY_HTTPS_HOST", $"localhost:{https}");
    Env.SetHostValue("UMBRACO_PUBLIC_HOST", $"https://localhost:{https}");
    Env.SetHostValue("UMBRACO_PUBLIC_URL", $"https://localhost:{https}/");
    Env.SetHostValue("ARTICULATE_REDIRECT_URI", $"https://localhost:{https}/a-new/");
    Env.SetHostValue("ARTICULATE_LOGOUT_REDIRECT_URI", $"https://localhost:{https}/");
    Env.SetHostValue("CADDY_TLS_HOST", colon >= 0 ? host[..colon] : host);
}

async Task EnsurePackages(string lane)
{
    var dir = Path.Combine(Env.Repo, "build", "Release", lane);
    var ok = Directory.Exists(dir) && await HasCurrentPackages(dir, lane);
    if (!ok)
    {
        Env.Set("ARTICULATE_PACKAGE_LANE", lane);
        await BuildAsync(Opts.Of(("lane", lane), ("sample", "true")));
    }
}

async Task<bool> HasCurrentPackages(string dir, string lane)
{
    var version = await ResolvePackageVersion(lane);
    if (version is not null)
        return HasPackage(dir, "Articulate", version) &&
            HasPackage(dir, "Articulate.Theme.Sample", version);

    var nbgv = (await Capture("nbgv", new[] { "get-version", "-v", "SemVer2" }, Env.Repo)).Trim();
    var commitSuffix = Regex.Match(nbgv, @"g[a-f0-9]+$").Value;
    if (string.IsNullOrWhiteSpace(commitSuffix))
        return HasPackage(dir, "Articulate", nbgv) &&
            HasPackage(dir, "Articulate.Theme.Sample", nbgv);

    var major = lane == "v18" ? "7" : "6";
    return Directory.EnumerateFiles(dir, $"Articulate.{major}.*{commitSuffix}.nupkg").Any() &&
        Directory.EnumerateFiles(dir, $"Articulate.Theme.Sample.{major}.*{commitSuffix}.nupkg").Any();
}

bool HasPackage(string dir, string id, string version)
    => Directory.EnumerateFiles(dir, $"{id}.{version}.nupkg").Any();

async Task<string?> ResolvePackageVersion(string lane)
{
    var packageVersion = Env.Get("ARTICULATE_PACKAGE_VERSION");
    if (!string.IsNullOrWhiteSpace(packageVersion)) return packageVersion;
    if (lane != "v18") return null;

    var baseVersion = File.ReadAllText(Path.Combine(Env.Repo, "build", "v18-version.txt")).Trim();
    if (string.IsNullOrWhiteSpace(baseVersion))
        throw new InvalidOperationException("build/v18-version.txt is empty.");

    var v17 = (await Capture("nbgv", new[] { "get-version", "-v", "SemVer2" }, Env.Repo)).Trim();
    var commitMatch = Regex.Match(v17, @"[-.]g[a-f0-9]+$");
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

void DeleteExistingPackages(string releaseDir, string lane)
{
    if (!Directory.Exists(releaseDir)) return;
    var major = lane == "v18" ? "7" : "6";
    foreach (var file in Directory.EnumerateFiles(releaseDir))
    {
        var name = Path.GetFileName(file);
        if (Regex.IsMatch(name, $@"^Articulate(?:\.Theme\.Sample)?\.{major}\..*\.s?nupkg$"))
            File.Delete(file);
    }
}

// ---- CA trust ----

async Task<int> DockerCaAsync(Opts _)
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

    public static void Set(string name, string value)
        => Environment.SetEnvironmentVariable(name, value);

    public static void SetHostValue(string name, string fallback)
    {
        var value = HostOverrides[name];
        Environment.SetEnvironmentVariable(name, string.IsNullOrWhiteSpace(value) ? fallback : value);
    }

    public static string? HostOverride(string name)
    {
        var value = HostOverrides[name];
        return string.IsNullOrWhiteSpace(value) ? null : value;
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

    public static readonly Dictionary<string, string?> HostOverrides = new[]
    {
        "CADDY_HTTPS_PORT",
        "CADDY_HTTP_PORT",
        "CADDY_HTTPS_HOST",
        "CADDY_TLS_HOST",
        "UMBRACO_PUBLIC_HOST",
        "UMBRACO_PUBLIC_URL",
        "ARTICULATE_REDIRECT_URI",
        "ARTICULATE_LOGOUT_REDIRECT_URI",
    }.ToDictionary(name => name, Environment.GetEnvironmentVariable);

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

    public static Opts Of(params (string Key, string? Value)[] kvs)
    {
        var d = new Dictionary<string, string?>();
        foreach (var (k, v) in kvs) d[k] = v;
        return new Opts(d);
    }

    public Opts Validate(string command, params string[] allowed)
    {
        var unknown = _v.Keys.Where(key => !allowed.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (unknown.Length > 0)
            throw new ArgumentException(
                $"Unknown option(s) for {command}: {string.Join(", ", unknown.Select(x => $"--{x}"))}. " +
                $"Run 'dotnet run --file build/build.cs -- help {command}'.");

        RequireValue("lane");
        RequireValue("configuration");
        RequireValue("tag");
        RequireBoolean("tests");
        RequireBoolean("client");
        RequireBoolean("sample");
        RequireFlag("clean");
        RequireFlag("reset");
        RequireFlag("skip-smoke");
        RequireFlag("keep");

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

    public bool Bool(string key, bool fallback = false)
        => _v.TryGetValue(key, out var v) && v is not null
            ? bool.TryParse(v, out var b) ? b : Env.IsTrue(v)
            : fallback;

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
