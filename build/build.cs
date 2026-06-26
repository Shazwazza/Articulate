#!/usr/bin/env dotnet
#:property RunAnalyzers=false
#nullable enable

using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

return await BuildApp.RunAsync(args);

static class BuildApp
{
    static readonly string Repo = FindRepo();
    static readonly string BuildDir = Path.Combine(Repo, "build");
    static readonly string Solution = Path.Combine(Repo, "src", "Articulate.sln");
    static readonly string DockerDir = Path.Combine(BuildDir, "docker-site");
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
    }.ToDictionary(name => name, Env);

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help")
            {
                Help(null);
                return 0;
            }

            if (args[0] == "help")
            {
                Help(args.Length > 1 ? args[1] : null);
                return 0;
            }

            var command = args[0];
            var options = Options.Parse(args[1..]);
            switch (command)
            {
                case "build":
                    options.Validate(command, ["lane", "configuration", "tests", "client", "sample", "clean"]);
                    await BuildAsync(options);
                    break;
                case "client":
                    options.Validate(command, ["lane"]);
                    await ClientAsync(options);
                    break;
                case "site":
                    options.Validate(command, ["lane", "configuration", "reset"]);
                    await SiteAsync(options);
                    break;
                case "docker-build":
                    options.Validate(command, ["lane", "tag"]);
                    await DockerBuildAsync(options);
                    break;
                case "docker-dev":
                    options.Validate(command, ["lane", "skip-smoke", "reset"]);
                    await DockerDevAsync(options);
                    break;
                case "docker-prod":
                    options.Validate(command, ["lane"]);
                    await DockerProdAsync(options);
                    break;
                case "docker-status":
                    options.Validate(command, ["lane"]);
                    await DockerStatusAsync(options);
                    break;
                case "docker-test":
                    options.Validate(command, ["lane", "keep", "skip-smoke"]);
                    await DockerTestAsync(options);
                    break;
                default:
                throw new ArgumentException($"Unknown command '{command}'. Run 'dotnet run --file build/build.cs -- --help'.");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }

    static void Help(string? command)
    {
        var text = command switch
        {
            null =>
                """
                Articulate build utility

                Usage:
                  dotnet run --file build/build.cs -- <command> [options]
                  dotnet run --file build/build.cs -- help [command]

                Commands:
                  build          Restore, build, optionally test, and pack a package lane.
                  client         Install, check, build, and lint one Backoffice client.
                  site           Run the local Articulate.Tests.Website for one lane.
                  docker-build   Build the standalone chiseled Docker image.
                  docker-dev     Build and start a development stack; publish sample content.
                  docker-prod    Restart an existing lane in Production mode and smoke-test it.
                  docker-status  Inspect a running lane and verify packaged Backoffice assets.
                  docker-test    Run the complete Docker matrix for one or both lanes.

                Common defaults:
                  --lane v17     Used by all commands except docker-test.
                  docker-test    Defaults to --lane all.

                Run 'dotnet run --file build/build.cs -- help <command>' for
                command options, defaults, requirements, and behavior.
                """,
            "build" =>
                """
                build - restore, build, optionally test, and pack a package lane

                Usage:
                  dotnet run --file build/build.cs -- build [options]

                Options:
                  --lane v17|v18              Package lane. Default: v17.
                  --configuration Debug|Release
                                               Default: BUILD_CONFIGURATION or Release.
                  --tests [true|false]         Run tests. Default: true in CI, otherwise false.
                  --client true|false          Build client assets. Default: true in CI/Release.
                  --sample [true|false]        Pack sample theme. Default: true locally.
                  --clean                     Remove shared build/client outputs before building.

                Output:
                  build/<configuration>/<lane>/

                Environment:
                  ARTICULATE_PACKAGE_VERSION  Optional package-version override.
                  RUN_TESTS, ENABLE_CLIENT_BUILD, PACK_SAMPLE_THEME
                                               Boolean option fallbacks.
                """,
            "client" =>
                """
                client - install, typecheck, build, and lint a Backoffice client

                Usage:
                  dotnet run --file build/build.cs -- client [--lane v17|v18]

                Options:
                  --lane v17|v18              Default: v17.

                Runs pnpm install at the client workspace root, then check, build,
                and lint in the selected lane.
                """,
            "site" =>
                """
                site - run Articulate.Tests.Website for one package lane

                Usage:
                  dotnet run --file build/build.cs -- site [options]

                Options:
                  --lane v17|v18              Default: v17.
                  --configuration Debug|Release
                                               Default: Debug.
                  --reset                     Delete the site's umbraco data first.

                This command remains attached to the running site until stopped.
                """,
            "docker-build" =>
                """
                docker-build - build the standalone chiseled Docker image

                Usage:
                  dotnet run --file build/build.cs -- docker-build [options]

                Options:
                  --lane v17|v18              Default: v17.
                  --tag image:tag              Default: articulate-local:<lane>.

                Missing Articulate and sample-theme packages are built first.
                """,
            "docker-dev" =>
                """
                docker-dev - build and start a development Docker stack

                Usage:
                  dotnet run --file build/build.cs -- docker-dev [options]

                Options:
                  --lane v17|v18              Default: v17.
                  --reset                     Run docker compose down -v first.
                  --skip-smoke                Skip sample publish/confirm checks.

                Without --skip-smoke, ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET
                is required. Missing packages are built automatically.
                """,
            "docker-prod" =>
                """
                docker-prod - restart an existing lane in Production mode

                Usage:
                  dotnet run --file build/build.cs -- docker-prod [--lane v17|v18]

                Options:
                  --lane v17|v18              Default: v17.

                Requires ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET. Reuses the
                selected lane's volumes, then runs front-end and theme smoke tests.
                """,
            "docker-status" =>
                """
                docker-status - inspect a running Docker lane

                Usage:
                  dotnet run --file build/build.cs -- docker-status [--lane v17|v18]

                Options:
                  --lane v17|v18              Default: v17.

                Shows Compose status and verifies the packaged Backoffice bundle
                and umbraco-package.json inside the running container.
                """,
            "docker-test" =>
                """
                docker-test - run the complete Docker validation matrix

                Usage:
                  dotnet run --file build/build.cs -- docker-test [options]

                Options:
                  --lane v17|v18|all          Default: all.
                  --keep                      Leave successful stacks running.
                  --skip-smoke                Skip API, front-end, and theme smoke tests.

                Each lane builds without Docker cache, starts in development mode,
                and, unless smoke is skipped, publishes/confirms content then
                restarts in Production mode for front-end and theme checks.

                Without --skip-smoke, ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET
                is required.
                """,
            _ => throw new ArgumentException($"Unknown help topic '{command}'."),
        };

        Console.WriteLine(text);
    }

    static async Task BuildAsync(Options options)
    {
        var started = Stopwatch.StartNew();
        var lane = Lane(options);
        var configuration = options.Value("configuration") ?? Env("BUILD_CONFIGURATION") ?? "Release";
        var inCi = IsTrue(Env("CI")) || IsTrue(Env("GITHUB_ACTIONS"));
        var runTests = options.Flag("tests") || BoolOption(options, "tests", "RUN_TESTS", inCi);
        var clientBuild = BoolOption(options, "client", "ENABLE_CLIENT_BUILD", inCi || configuration == "Release");
        var sample = options.Flag("sample") || BoolOption(options, "sample", "PACK_SAMPLE_THEME", !inCi);
        var clientVersion = lane == "v18" ? "18" : "17";
        var major = lane == "v18" ? "7" : "6";
        var releaseDir = Path.Combine(BuildDir, configuration, lane);
        Directory.CreateDirectory(releaseDir);

        var packageVersion = Env("ARTICULATE_PACKAGE_VERSION");
        if (lane == "v18" && string.IsNullOrWhiteSpace(packageVersion))
        {
            // Local fallback. CI sets ARTICULATE_PACKAGE_VERSION from
            // NBGV_SemVer2, so this branch only runs for un-set local builds.
            var baseVersion = (await File.ReadAllTextAsync(Path.Combine(BuildDir, "v18-version.txt"))).Trim();
            if (string.IsNullOrWhiteSpace(baseVersion))
                throw new InvalidOperationException("build/v18-version.txt is empty.");
            var v17 = (await CaptureAsync("nbgv", ["get-version", "-v", "SemVer2"], Repo)).Trim();
            // Release-tagged commits produce clean NBGV semver (e.g. "7.0.0")
            // with no commit-hash suffix. In that case, baseVersion alone is
            // the final v18 version.
            var commitMatch = Regex.Match(v17, @"\.g[a-f0-9]+$");
            packageVersion = baseVersion + commitMatch.Value;
        }

        foreach (var file in Directory.EnumerateFiles(releaseDir))
            if (Regex.IsMatch(Path.GetFileName(file), $@"^Articulate(?:\.Theme\.Sample)?\.{major}\..*\.s?nupkg$"))
                File.Delete(file);

        var props = new List<string>
        {
            $"-p:EnableClientBuild={clientBuild.ToString().ToLowerInvariant()}",
            $"-p:ArticulatePackageLane={lane}",
            $"-p:UmbracoClientVersion={clientVersion}",
        };
        if (!string.IsNullOrWhiteSpace(packageVersion))
            props.Add($"-p:ArticulatePackageVersion={packageVersion}");

        var clean = options.Flag("clean");
        Console.WriteLine($"Build: {lane}, {configuration}, package {packageVersion ?? "NBGV"}{(clean ? " (Clean Build)" : "")}");
        Environment.SetEnvironmentVariable("CI", "true");
        
        if (clean)
        {
            await RunAsync("dotnet", ["build-server", "shutdown"], Repo, allowFailure: true);
            DeleteBuildOutputs(Path.Combine(Repo, "src"));
            DeleteDirectory(Path.Combine(BuildDir, "ClientAssets"));
        }

        // v17 and v18 share the same Vite output dir but have per-lane
        // stamps, so without this a v18 build leaks its bundle into a
        // subsequent v17 pack. See DEVELOP.md "Package lanes" for the
        // TODO follow-up.
        DeleteDirectory(Path.Combine(Repo, "src", "Articulate.Web", "wwwroot", "App_Plugins", "Articulate", "BackOffice"));

        if (clientBuild)
        {
            var clientRoot = Path.Combine(Repo, "src", "Articulate.Web", "Client");
            if (clean)
            {
                await RunAsync("pnpm", ["--workspace-concurrency=1", "-r", "run", "clean"], clientRoot);
                DeleteDirectory(Path.Combine(clientRoot, "node_modules"));
            }
            
            var pnpmArgs = inCi
                ? new[] { "install", "--frozen-lockfile", "--prefer-offline" }
                : new[] { "install", "--prefer-offline" };
            await RunAsync("pnpm", pnpmArgs, clientRoot);
        }

        var restoreArgs = new List<string> { "restore", Solution, "-v", "minimal", "-p:RestoreUseStaticGraphEvaluation=true" };
        if (inCi)
        {
            restoreArgs.Add("--locked-mode");
        }
        restoreArgs.AddRange(props);
        await RunAsync("dotnet", [.. restoreArgs], Repo);
        await RunAsync("dotnet", ["build", Solution, "-c", configuration, "--no-restore", "-v", "minimal",
            "-m:1", "-p:BuildInParallel=false", "-p:UseSharedCompilation=false", .. props], Repo);
        if (runTests)
            await RunAsync("dotnet", ["test", Solution, "-c", configuration, "--no-restore", "--no-build",
                "-v", "minimal", "-m:1", "-p:BuildInParallel=false", .. props], Repo);

        var projects = new List<string> { Path.Combine(Repo, "src", "Articulate.Web", "Articulate.Web.csproj") };
        if (sample) projects.Add(Path.Combine(Repo, "src", "Articulate.Theme.Sample", "Articulate.Theme.Sample.csproj"));
        foreach (var project in projects)
            await RunAsync("dotnet", ["pack", project, "-c", configuration, "--no-restore", "--no-build",
                "-o", releaseDir, "-v", "minimal", "-m:1", "-p:BuildInParallel=false", .. props], Repo);

        Console.WriteLine($"Completed in {started.Elapsed.TotalSeconds:N1}s: {releaseDir}");
    }

    static async Task ClientAsync(Options options)
    {
        var lane = Lane(options);
        var root = Path.Combine(Repo, "src", "Articulate.Web", "Client");
        await RunAsync("pnpm", ["install"], root);
        var dir = Path.Combine(root, lane);
        await RunAsync("pnpm", ["run", "check"], dir);
        await RunAsync("pnpm", ["run", "build"], dir);
        await RunAsync("pnpm", ["run", "lint"], dir);
    }

    static async Task SiteAsync(Options options)
    {
        var lane = Lane(options);
        var configuration = options.Value("configuration") ?? "Debug";
        var project = Path.Combine(Repo, "src", "Articulate.Tests.Website", "Articulate.Tests.Website.csproj");
        if (options.Flag("reset"))
            DeleteDirectory(Path.Combine(Repo, "src", "Articulate.Tests.Website", "umbraco"));
        await RunAsync("dotnet", ["run", "-c", configuration, "--project", project,
            $"-p:ArticulatePackageLane={lane}"], Repo);
    }

    static async Task DockerBuildAsync(Options options)
    {
        var lane = Lane(options);
        await EnsurePackagesAsync(lane);
        var tag = options.Value("tag") ?? $"articulate-local:{lane}";
        var version = lane == "v18" ? "[18.0.0-*,19.0.0)" : "[17.4.0,18.0.0)";
        await RunAsync("docker", ["build", "--file", "Dockerfile", "--target", "chiseled", "--tag", tag,
            "--build-arg", $"PACKAGE_SOURCE=build/Release/{lane}", "--build-arg", $"UMBRACO_CMS_VERSION={version}", "."], Repo);
    }

    static async Task DockerDevAsync(Options options)
    {
        Require("docker");
        var lane = Lane(options);
        await EnsurePackagesAsync(lane);
        ConfigureLane(lane);
        var url = Env("UMBRACO_PUBLIC_URL")!;
        Environment.SetEnvironmentVariable("UMBRACO_RUNTIME_MODE", "BackofficeDevelopment");
        if (options.Flag("reset")) await ComposeAsync(["down", "-v"]);
        await ComposeAsync(["up", "-d", "--build"]);
        await WaitForAsync(new Uri(new Uri(url), "umbraco/"));
        if (!options.Flag("skip-smoke"))
        {
            RequireSecret();
            await SmokeAsync("publish");
            await SmokeAsync("confirm");
        }
    }

    static async Task DockerProdAsync(Options options)
    {
        RequireSecret();
        ConfigureLane(Lane(options));
        Environment.SetEnvironmentVariable("UMBRACO_RUNTIME_MODE", "Production");
        await ComposeAsync(["up", "-d", "--force-recreate"]);
        await WaitForAsync(new Uri(new Uri(Env("UMBRACO_PUBLIC_URL")!), "umbraco/"));
        await SmokeAsync("smoke");
        await SmokeAsync("theme");
    }

    static async Task DockerStatusAsync(Options options)
    {
        Require("docker");
        ConfigureLane(Lane(options));
        await ComposeAsync(["ps"]);
        var containerId = (await CaptureAsync(
            "docker",
            ["compose", "ps", "-q", "articulate"],
            Repo)).Trim();
        if (string.IsNullOrWhiteSpace(containerId))
            throw new InvalidOperationException("The articulate container is not running.");

        var inspectionDir = Path.Combine(Path.GetTempPath(), $"articulate-docker-status-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(inspectionDir);
            await RunAsync(
                "docker",
                ["cp", $"{containerId}:/app/wwwroot/App_Plugins/Articulate/.", inspectionDir],
                Repo);

            var backofficeBundle = Directory
                .EnumerateFiles(inspectionDir, "articulate-backoffice.js", SearchOption.AllDirectories)
                .FirstOrDefault();
            var manifest = Directory
                .EnumerateFiles(inspectionDir, "umbraco-package.json", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (backofficeBundle is null || manifest is null)
                throw new InvalidOperationException(
                    "The running articulate container is missing the Backoffice bundle or umbraco-package.json.");

            Console.WriteLine($"Backoffice bundle: {Path.GetRelativePath(inspectionDir, backofficeBundle)}");
            Console.WriteLine($"Package manifest: {Path.GetRelativePath(inspectionDir, manifest)}");
        }
        finally
        {
            DeleteDirectory(inspectionDir);
        }
    }

    static async Task DockerTestAsync(Options options)
    {
        var requested = options.Value("lane") ?? "all";
        if (requested is not ("v17" or "v18" or "all"))
            throw new ArgumentException("--lane must be v17, v18, or all.");
        var lanes = requested == "all" ? new[] { "v17", "v18" } : new[] { requested };
        var keep = options.Flag("keep");
        var skipSmoke = options.Flag("skip-smoke");

        foreach (var lane in lanes)
        {
            await EnsurePackagesAsync(lane);
            ConfigureLane(lane);
            try
            {
                await ComposeAsync(["build", "--no-cache", "--pull"]);
                await DockerDevAsync(Options.FromFlags(skipSmoke ? ["skip-smoke"] : []));
                if (!skipSmoke) await DockerProdAsync(new Options());
                Console.WriteLine($"PASSED: {lane}");
            }
            finally
            {
                if (!keep) await ComposeAsync(["down", "-v"], allowFailure: true);
            }
        }
    }

    static async Task EnsurePackagesAsync(string lane)
    {
        var major = lane == "v18" ? "7" : "6";
        var dir = Path.Combine(BuildDir, "Release", lane);
        var package = Directory.Exists(dir) && Directory.EnumerateFiles(dir, $"Articulate.{major}.*.nupkg").Any();
        var sample = Directory.Exists(dir) && Directory.EnumerateFiles(dir, $"Articulate.Theme.Sample.{major}.*.nupkg").Any();
        if (!package || !sample)
            await BuildAsync(Options.FromValues(("lane", lane), ("sample", "true")));
    }

    static void ConfigureLane(string lane)
    {
        var is18 = lane == "v18";
        // HTTPS 44317/44318 match the Umbraco major; HTTP 44380/44381 avoid
        // common dev-tool port-snatch ranges. Override with CADDY_HTTPS_PORT /
        // CADDY_HTTP_PORT before invoking the build script.
        var https = is18 ? "44318" : "44317";
        var http = is18 ? "44381" : "44380";
        Environment.SetEnvironmentVariable("ARTICULATE_PACKAGE_LANE", lane);
        Environment.SetEnvironmentVariable("COMPOSE_PROJECT_NAME", $"art_{lane}");
        Environment.SetEnvironmentVariable("COMPOSE_VOLUME_PREFIX", $"art_{lane}");
        Environment.SetEnvironmentVariable("IMAGE_TAG", $"articulate-local:{lane}");
        Environment.SetEnvironmentVariable("PACKAGE_SOURCE", $"build/Release/{lane}");
        Environment.SetEnvironmentVariable("UMBRACO_CMS_VERSION", is18 ? "[18.0.0-*,19.0.0)" : "[17.4.0,18.0.0)");
        // Per-lane auth cookies: cookies are domain-scoped, so the default
        // back-office cookie would clobber itself across lanes on localhost.
        Environment.SetEnvironmentVariable("Umbraco__CMS__Security__AuthCookieName", $"UMB_UCONTEXT-{lane}");
        // Umbraco 17.3+ OAuth cookies (PR #22057): SiteName is appended verbatim.
        Environment.SetEnvironmentVariable("Umbraco__CMS__Security__BackOfficeTokenCookie__SiteName", $"-{lane}");
        SetHostValue("CADDY_HTTPS_PORT", https);
        SetHostValue("CADDY_HTTP_PORT", http);
        SetHostValue("CADDY_HTTPS_HOST", $"localhost:{https}");
        SetHostValue("UMBRACO_PUBLIC_HOST", $"https://localhost:{https}");
        SetHostValue("UMBRACO_PUBLIC_URL", $"https://localhost:{https}/");
        SetHostValue("ARTICULATE_REDIRECT_URI", $"https://localhost:{https}/a-new/");
        SetHostValue("ARTICULATE_LOGOUT_REDIRECT_URI", $"https://localhost:{https}/");

        var caddyHttpsHost = Env("CADDY_HTTPS_HOST") ?? $"localhost:{https}";
        var caddyTlsHost = caddyHttpsHost;
        var portIndex = caddyHttpsHost.LastIndexOf(':');
        if (portIndex >= 0)
        {
            caddyTlsHost = caddyHttpsHost[..portIndex];
        }
        SetHostValue("CADDY_TLS_HOST", caddyTlsHost);
    }

    static async Task ComposeAsync(IEnumerable<string> args, bool allowFailure = false) =>
        await RunAsync("docker", ["compose", .. args], Repo, allowFailure);

    static async Task SmokeAsync(string command)
    {
        var node = Env("NODE_BIN") ?? "node";
        await RunAsync(node, [Path.Combine(DockerDir, "smoke.mjs"), command], Repo);
    }

    static async Task WaitForAsync(Uri uri)
    {
        using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(uri);
                if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Found) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(2000);
        }
        throw new TimeoutException($"Timed out waiting for {uri}.");
    }

    static async Task RunAsync(string file, IEnumerable<string> args, string cwd, bool allowFailure = false)
    {
        Require(file);
        var info = new ProcessStartInfo(file) { WorkingDirectory = cwd, UseShellExecute = false };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        Console.WriteLine($"> {file} {string.Join(' ', info.ArgumentList)}");
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {file}.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0 && !allowFailure)
            throw new InvalidOperationException($"{file} exited with code {process.ExitCode}.");
    }

    static async Task<string> CaptureAsync(string file, IEnumerable<string> args, string cwd)
    {
        Require(file);
        var info = new ProcessStartInfo(file) { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {file}.");
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"{file} exited with code {process.ExitCode}.");
        return output;
    }

    static void DeleteBuildOutputs(string root)
    {
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .Where(path => Path.GetFileName(path) is "bin" or "obj")
                     .OrderByDescending(path => path.Length).ToArray())
            DeleteDirectory(dir);
    }

    static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    static string Lane(Options options)
    {
        var lane = (options.Value("lane") ?? Env("ARTICULATE_PACKAGE_LANE") ?? "v17").ToLowerInvariant();
        if (lane is not ("v17" or "v18")) throw new ArgumentException("Lane must be v17 or v18.");
        return lane;
    }

    static bool BoolOption(Options options, string key, string env, bool fallback) =>
        options.Value(key) is { } value ? IsTrue(value) :
        Env(env) is { } envValue ? IsTrue(envValue) : fallback;

    static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    static string? Env(string name) => Environment.GetEnvironmentVariable(name);
    static void SetDefault(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(Env(name))) Environment.SetEnvironmentVariable(name, value);
    }
    static void SetHostValue(string name, string fallback)
    {
        var value = HostOverrides[name];
        Environment.SetEnvironmentVariable(name, string.IsNullOrWhiteSpace(value) ? fallback : value);
    }
    static void RequireSecret()
    {
        // Default matches the docker-compose.yml fallback so local dev works
        // without exporting anything. Override explicitly in CI.
        SetDefault("ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET", "articulate-dev-local-secret");
        SetDefault("ARTICULATE_DEV_AUTOMATION_CLIENT_ID", "articulate-dev-automation");
    }
    static void Require(string command)
    {
        if (Path.IsPathRooted(command) && File.Exists(command)) return;
        var names = OperatingSystem.IsWindows() ? new[] { command, $"{command}.exe", $"{command}.cmd" } : [command];
        if ((Env("PATH") ?? "").Split(Path.PathSeparator).Any(dir => names.Any(name => File.Exists(Path.Combine(dir, name))))) return;
        throw new InvalidOperationException($"Required command not found on PATH: {command}");
    }

    static string FindRepo()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "Articulate.Web")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Run this command from the Articulate repository.");
    }
}

sealed class Options
{
    readonly Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);
    public static Options Parse(string[] args)
    {
        var result = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            var key = args[i][2..];
            result.values[key] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : null;
        }
        return result;
    }
    public static Options FromFlags(IEnumerable<string> flags)
    {
        var result = new Options();
        foreach (var flag in flags) result.values[flag] = null;
        return result;
    }
    public static Options FromValues(params (string Key, string Value)[] values)
    {
        var result = new Options();
        foreach (var (key, value) in values) result.values[key] = value;
        return result;
    }
    public void Validate(string command, IReadOnlyCollection<string> allowed)
    {
        string[] unknown = values.Keys.Where(key => !allowed.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (unknown.Length > 0)
            throw new ArgumentException(
                $"Unknown option(s) for {command}: {string.Join(", ", unknown.Select(x => $"--{x}"))}. " +
                $"Run 'dotnet run --file build/build.cs -- help {command}'.");

        RequireValue("lane");
        RequireValue("configuration");
        RequireValue("client");
        RequireValue("tag");
        RequireBoolean("tests");
        RequireBoolean("client");
        RequireBoolean("sample");
        RequireFlag("clean");
        RequireFlag("reset");
        RequireFlag("skip-smoke");
        RequireFlag("keep");

        if (Value("configuration") is { } configuration &&
            configuration is not ("Debug" or "Release"))
            throw new ArgumentException("--configuration must be Debug or Release.");
    }
    void RequireValue(string key)
    {
        if (values.ContainsKey(key) && string.IsNullOrWhiteSpace(values[key]))
            throw new ArgumentException($"--{key} requires a value.");
    }
    void RequireBoolean(string key)
    {
        if (Value(key) is { } value && !bool.TryParse(value, out _))
            throw new ArgumentException($"--{key} must be true or false.");
    }
    void RequireFlag(string key)
    {
        if (Value(key) is not null)
            throw new ArgumentException($"--{key} is a flag and does not accept a value.");
    }
    public string? Value(string key) => values.TryGetValue(key, out var value) ? value : null;
    public bool Flag(string key) => values.ContainsKey(key) && values[key] is null;
}
