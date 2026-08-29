#!/usr/bin/env -S dotnet --
#:property NoWarn=SA1400,SA1503,SA1519,SA1116,SA1117,SA1122,SA1649,IDE0008,IDE0011,IDE0040,SA1500

# nullable enable

// Articulate Docker utility. CLI reference lives in docker/help.md.

using System.Diagnostics;
using System.Net;

try
{
    if (args.Length == 0 || args[0] is "-h" or "--help") { Help(null); return 0; }
    if (args[0] == "help") { Help(args.Length > 1 ? args[1] : null); return 0; }

    var command = args[0];
    var opts = Opts.Parse(args[1..]);
    return command switch
    {
        "docker-build"  => await DockerBuild(opts.Validate(command, "lane", "tag", "clean")),
        "docker-dev"    => await DockerDev(opts.Validate(command, "lane", "skip-smoke", "reset", "clean", "reuse-packages")),
        "docker-prod"   => await DockerProd(opts.Validate(command, "lane", "skip-smoke")),
        "docker-down"   => await DockerDown(opts.Validate(command, "lane", "volumes", "purge")),
        "docker-status" => await DockerStatus(opts.Validate(command, "lane")),
        "docker-test"   => await DockerTest(opts.Validate(command, "lane", "keep", "skip-smoke")),
        "docker-ca"     => await DockerCa(opts.Validate(command, "lane")),
        _ => throw new ArgumentException($"Unknown command '{command}'. Run with --help.")
    };
}
catch (Exception e) { Console.Error.WriteLine($"ERROR: {e.Message}"); return 1; }

void Help(string? topic)
{
    var text = File.ReadAllText(Path.Combine(Env.Repo, "docker", "help.md"));
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

async Task<int> DockerBuild(Opts o)
{
    var lane = ConfigureLane(o.Lane());
    await EnsurePackages(lane, o.Flag("clean"));
    await Run("docker", new[]
    {
        "build", "--file", "docker/Dockerfile", "--target", "chiseled", "--tag", o.String("tag", $"articulate-local:{lane}")!,
        "--build-arg", $"PACKAGE_SOURCE={Env.Get("PACKAGE_SOURCE", $"build/Release/{lane}")}",
        "--build-arg", $"UMBRACO_CMS_VERSION={Env.Get("UMBRACO_CMS_VERSION")}",
        "--build-arg", $"USE_TINYMCE_UMBRACO={Env.Get("USE_TINYMCE_UMBRACO", "false")}",
        "--build-arg", $"TINYMCE_UMBRACO_PACKAGE_VERSION={Env.Get("TINYMCE_UMBRACO_PACKAGE_VERSION")}",
        "."
    }, Env.Repo);
    return 0;
}

async Task<int> DockerDev(Opts o, bool build = true, bool ensurePackages = true)
{
    var lane = ConfigureLane(o.Lane());
    var reusePackages = o.Flag("reuse-packages");
    if (reusePackages && o.Flag("clean"))
        throw new ArgumentException("--clean cannot be used with --reuse-packages.");
    if (ensurePackages && !reusePackages) await EnsurePackages(lane, o.Flag("clean"));
    Env.Set("UMBRACO_RUNTIME_MODE", "BackofficeDevelopment");
    if (o.Flag("reset")) await Compose(new[] { "down", "--volumes" }, allowFailure: true);
    await Compose(build ? new[] { "up", "--detach", "--build" } : new[] { "up", "--detach" });
    await WaitForPublicSite();
    if (!o.Flag("skip-smoke"))
    {
        Env.RequireSecret();
        await Smoke("publish");
        await Smoke("confirm");
    }
    return 0;
}

async Task<int> DockerProd(Opts o)
{
    ConfigureLane(o.Lane());
    Env.RequireSecret();
    Env.Set("UMBRACO_RUNTIME_MODE", "Production");
    await Compose(new[] { "up", "--detach", "--force-recreate" });
    await WaitForPublicSite();
    if (!o.Flag("skip-smoke"))
    {
        await Smoke("smoke");
        await Smoke("theme");
    }
    return 0;
}

async Task<int> DockerDown(Opts o)
{
    var args = o.Flag("purge")
        ? new[] { "down", "--volumes", "--rmi", "all", "--remove-orphans" }
        : o.Flag("volumes")
            ? new[] { "down", "--volumes" }
            : new[] { "down" };
    var lanes = string.Equals(o.String("lane"), "all", StringComparison.OrdinalIgnoreCase)
        ? new[] { "v17", "v18" }
        : new[] { o.Lane() };
    foreach (var lane in lanes)
    {
        ConfigureLane(lane);
        await Compose(args);
    }
    return 0;
}

async Task<int> DockerStatus(Opts o)
{
    ConfigureLane(o.Lane());
    await Compose(new[] { "ps" });
    var id = (await Capture("docker", ComposeArgs("ps", "-q", "articulate"), Env.Repo)).Trim();
    if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("articulate container is not running.");

    var dir = Path.Combine(Path.GetTempPath(), $"art-status-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);
    try
    {
        await Run("docker", new[] { "cp", $"{id}:/app/wwwroot/App_Plugins/Articulate/.", dir }, Env.Repo);
        var bundle = Directory.EnumerateFiles(dir, "articulate-backoffice.js", SearchOption.AllDirectories).FirstOrDefault();
        var manifest = Directory.EnumerateFiles(dir, "umbraco-package.json", SearchOption.AllDirectories).FirstOrDefault();
        if (bundle is null || manifest is null) throw new InvalidOperationException("Container is missing Backoffice bundle or umbraco-package.json.");
        Console.WriteLine($"Backoffice bundle: {Path.GetRelativePath(dir, bundle)}");
        Console.WriteLine($"Package manifest:  {Path.GetRelativePath(dir, manifest)}");
    }
    finally { DeleteDir(dir); }
    return 0;
}

async Task<int> DockerTest(Opts o)
{
    var requested = (o.String("lane", "all") ?? "all").ToLowerInvariant();
    if (requested is not ("v17" or "v18" or "all"))
        throw new ArgumentException("--lane must be v17, v18, or all.");

    var lanes = requested == "all" ? new[] { "v17", "v18" } : new[] { requested };
    foreach (var lane in lanes)
    {
        ConfigureLane(lane);
        await EnsurePackages(lane, clean: true);
        try
        {
            await Compose(new[] { "build", "--no-cache", "--pull" });
            var devOptions = o.Flag("skip-smoke")
                ? Opts.Of(("lane", lane), ("skip-smoke", null))
                : Opts.Of(("lane", lane));
            await DockerDev(devOptions, build: false, ensurePackages: false);
            if (!o.Flag("skip-smoke")) await DockerProd(Opts.Of(("lane", lane)));
            Console.WriteLine($"PASSED: {lane}");
        }
        finally
        {
            if (!o.Flag("keep")) await Compose(new[] { "down", "--volumes" }, allowFailure: true);
        }
    }
    return 0;
}

async Task<int> DockerCa(Opts o)
{
    ConfigureLane(o.Lane());
    var id = (await Capture("docker", ComposeArgs("ps", "-q", "caddy"), Env.Repo)).Trim();
    if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Could not find a running 'caddy' container. Run 'docker-dev' first.");

    const string sourceProbe = "for f in /data/caddy/pki/authorities/local/root.crt /data/pki/authorities/local/root.crt; do [ -f \"$f\" ] && echo \"$f\" && exit 0; done; exit 1";
    var source = (await Capture("docker", new[] { "exec", id, "sh", "-lc", sourceProbe }, Env.Repo)).Trim();
    var output = Path.Combine(Env.Repo, "docker", "caddy-local-root.crt");
    await Run("docker", new[] { "cp", "--follow-link", $"{id}:{source}", output }, Env.Repo);
    if (OperatingSystem.IsWindows())
    {
        await Run("pwsh", new[] { "-NoProfile", "-Command", $"Import-Certificate -FilePath '{output}' -CertStoreLocation Cert:\\CurrentUser\\Root | Out-Null" }, Env.Repo);
    }
    else
    {
        await Run("sudo", new[] { "cp", output, "/usr/local/share/ca-certificates/caddy-local-root.crt" }, Env.Repo);
        await Run("sudo", new[] { "update-ca-certificates" }, Env.Repo);
    }
    Console.WriteLine($"Trusted Caddy root CA: {output}");
    return 0;
}

string ConfigureLane(string lane)
{
    var https = Env.HostValue("CADDY_HTTPS_PORT", lane == "v18" ? "44318" : "44317");
    var http = Env.HostValue("CADDY_HTTP_PORT", lane == "v18" ? "44381" : "44380");
    var configuration = Env.Get("BUILD_CONFIGURATION")?.Trim();
    if (string.IsNullOrWhiteSpace(configuration)) configuration = "Release";
    foreach (var (key, value) in new Dictionary<string, string>
    {
        ["ARTICULATE_PACKAGE_LANE"] = lane,
        ["COMPOSE_PROJECT_NAME"] = $"art_{lane}",
        ["COMPOSE_VOLUME_PREFIX"] = $"art_{lane}",
        ["IMAGE_TAG"] = $"articulate-local:{lane}",
        ["PACKAGE_SOURCE"] = $"build/{configuration}/{lane}",
        ["UMBRACO_CMS_VERSION"] = Env.MsbuildProperty("UmbracoCmsPackageVersion", lane),
        ["TINYMCE_UMBRACO_PACKAGE_VERSION"] = Env.MsbuildProperty("TinyMceUmbracoPackageVersion", lane),
        ["Umbraco__CMS__Security__AuthCookieName"] = $"UMB_UCONTEXT-{lane}",
        ["Umbraco__CMS__Security__BackOfficeTokenCookie__SiteName"] = $"-{lane}",
        ["CADDY_HTTPS_PORT"] = https,
        ["CADDY_HTTP_PORT"] = http,
        ["CADDY_HTTPS_HOST"] = $"localhost:{https}",
        ["UMBRACO_PUBLIC_HOST"] = $"https://localhost:{https}",
        ["UMBRACO_PUBLIC_URL"] = $"https://localhost:{https}/",
        ["ARTICULATE_REDIRECT_URI"] = $"https://localhost:{https}/a-new/",
        ["ARTICULATE_LOGOUT_REDIRECT_URI"] = $"https://localhost:{https}/"
    })
        Env.SetHostValue(key, value);

    var host = Env.Get("CADDY_HTTPS_HOST") ?? $"localhost:{https}";
    var separator = host.LastIndexOf(':');
    Env.SetHostValue("CADDY_TLS_HOST", separator >= 0 ? host[..separator] : host);
    return lane;
}

Task EnsurePackages(string lane, bool clean = false)
{
    var args = new List<string> { "run", "build/build.cs", "--", "build", "--lane", lane, "--sample" };
    if (clean) args.Add("--clean");
    return Run("dotnet", args, Env.Repo);
}

async Task WaitForPublicSite()
{
    var url = Env.Get("UMBRACO_PUBLIC_URL") ?? "https://localhost:44317/";
    using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    var deadline = DateTime.UtcNow.AddMinutes(5);
    while (DateTime.UtcNow < deadline)
    {
        try
        {
            using var response = await client.GetAsync(new Uri(new Uri(url), "umbraco/"));
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Found) return;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        await Task.Delay(2000);
    }
    throw new TimeoutException($"Timed out waiting for {url}umbraco/.");
}

Task Smoke(string command) => Run(Env.Get("NODE_BIN", "node")!, new[] { Path.Combine(Env.Repo, "docker", "smoke.mjs"), command }, Env.Repo);
Task Compose(IEnumerable<string> args, bool allowFailure = false) => Run("docker", ComposeArgs(args.ToArray()), Env.Repo, allowFailure);
string[] ComposeArgs(params string[] args) => new[] { "compose", "-f", Path.Combine(Env.Repo, "docker", "docker-compose.yml") }.Concat(args).ToArray();

async Task Run(string file, IEnumerable<string> args, string? cwd = null, bool allowFailure = false)
{
    var psi = new ProcessStartInfo(file) { UseShellExecute = false };
    if (cwd is not null) psi.WorkingDirectory = cwd;
    foreach (var arg in args) psi.ArgumentList.Add(arg);
    Console.WriteLine($"> {file} {string.Join(' ', args)}");
    using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}.");
    await process.WaitForExitAsync();
    if (process.ExitCode != 0 && !allowFailure)
        throw new InvalidOperationException($"{file} exited {process.ExitCode}.");
}

async Task<string> Capture(string file, IEnumerable<string> args, string cwd)
{
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

static class Env
{
    public static readonly string Repo = FindRepo();
    static readonly Dictionary<string, string?> HostOverrides = new[]
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

    public static string? Get(string name, string? fallback = null)
        => Environment.GetEnvironmentVariable(name) ?? fallback;

    public static void Set(string name, string value) => Environment.SetEnvironmentVariable(name, value);

    public static string HostValue(string name, string fallback)
        => string.IsNullOrWhiteSpace(HostOverrides.GetValueOrDefault(name)) ? fallback : HostOverrides[name]!;

    public static void SetHostValue(string name, string fallback) => Set(name, HostValue(name, fallback));

    public static void RequireSecret()
    {
        if (string.IsNullOrWhiteSpace(Get("ARTICULATE_TEST_SITE_CLIENT_SECRET")))
            Set("ARTICULATE_TEST_SITE_CLIENT_SECRET", "articulate-test-site-secret");
    }

    public static string MsbuildProperty(string name, string lane)
    {
        var project = Path.Combine(Repo, "src", "Articulate.Web", "Articulate.Web.csproj");
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Repo,
        };
        foreach (var arg in new[] { "msbuild", project, $"-getProperty:{name}", $"-p:ArticulatePackageLane={lane}" })
            psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start dotnet msbuild.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException($"Could not resolve MSBuild property '{name}' for lane '{lane}'.");
        return output;
    }

    static string FindRepo()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, "docker")))
                return directory.FullName;
        throw new InvalidOperationException("Run from the Articulate repository.");
    }
}

sealed class Opts
{
    readonly Dictionary<string, string?> _values;
    Opts(Dictionary<string, string?> values) { this._values = values; }

    public static Opts Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            var key = args[i][2..];
            values[key] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : null;
        }
        return new Opts(values);
    }

    public static Opts Of(params (string Key, string? Value)[] options)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in options) values[key] = value;
        return new Opts(values);
    }

    public Opts Validate(string command, params string[] allowed)
    {
        var unknown = _values.Keys.Where(key => !allowed.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (unknown.Length > 0)
            throw new ArgumentException(
                $"Unknown option(s) for {command}: {string.Join(", ", unknown.Select(key => $"--{key}"))}. " +
                $"Run 'dotnet run docker/run.cs -- help {command}'.");

        RequireValue("lane");
        RequireValue("tag");
        RequireFlag("clean");
        RequireFlag("reset");
        RequireFlag("skip-smoke");
        RequireFlag("keep");
        RequireFlag("volumes");
        RequireFlag("purge");
        if (Flag("volumes") && Flag("purge"))
            throw new ArgumentException("--volumes and --purge cannot be used together.");
        return this;
    }

    public string Lane() => (String("lane") ?? Env.Get("ARTICULATE_PACKAGE_LANE", "v17") ?? "v17").ToLowerInvariant() switch
    {
        "v17" => "v17",
        "v18" => "v18",
        var lane => throw new ArgumentException($"--lane must be v17 or v18 (got '{lane}').")
    };

    public string? String(string key, string? fallback = null) => _values.TryGetValue(key, out var value) ? value : fallback;
    public bool Flag(string key) => _values.TryGetValue(key, out var value) && value is null;

    void RequireValue(string key)
    {
        if (_values.ContainsKey(key) && string.IsNullOrWhiteSpace(_values[key]))
            throw new ArgumentException($"--{key} requires a value.");
    }

    void RequireFlag(string key)
    {
        if (String(key) is not null)
            throw new ArgumentException($"--{key} is a flag and does not accept a value.");
    }
}
